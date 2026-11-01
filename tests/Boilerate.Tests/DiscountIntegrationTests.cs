using Boilerate.Application.Catalog;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Ordering;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Ordering;
using Boilerate.Infrastructure.Commerce;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class DiscountIntegrationTests
{
    private sealed class TestClock(DateTimeOffset value) : TimeProvider
    {
        public DateTimeOffset Current { get; set; } = value;
        public override DateTimeOffset GetUtcNow() => Current;
    }

    private static IUserService Users()
    {
        var users = Substitute.For<IUserService>();
        users.HasPermissionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        return users;
    }

    private static CommerceService Catalog(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return new(db, new CommerceUnitOfWork(db), scope.ServiceProvider.GetRequiredService<ICurrentUser>(), Users(),
            Options.Create(new CommerceSettings()), scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>());
    }

    private static OrderingService Ordering(IServiceScope scope, TimeProvider clock)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return new(db, new CommerceUnitOfWork(db), scope.ServiceProvider.GetRequiredService<ICurrentUser>(), Users(),
            Options.Create(new OrderingSettings { ReservationMinutes = 1 }), clock,
            scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>());
    }

    private static Checkout Checkout(CartDto cart) => new(cart.Version!, Guid.NewGuid(), OrderPaymentMethod.Prepaid,
        "Buyer", "buyer@example.test", "0901234567", "Address", cart.Total);

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task UsageIsAtomicReleasedOnExpiryAndLatePaymentWaitsWhenQuotaIsTaken(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var clock = new TestClock(new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.Zero));
        using var firstScope = fixture.Scope(Guid.NewGuid());
        using var secondScope = fixture.Scope(Guid.NewGuid());
        var db = firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalog = Catalog(firstScope);
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("DISCOUNT", "Discount test", []), default);
        var draft = await catalog.CreateAsync(new(type.Id, type.Version, new("Discount product", "discount-product", null, [], [], []),
            [], [new("DISCOUNT-SKU", 100, null, new())]), default);
        var product = await catalog.StatusAsync(new(draft.Id, draft.Version, ProductStatus.Published), default);
        var variant = product.Variants.Single().Id;
        var balance = (await catalog.InventoryAsync(1, 10, default, variantId: variant)).Items.Single();
        await catalog.AdjustAsync(new(balance.Id, balance.Version, Guid.NewGuid(), 5, "Discount test stock"), default);
        var discounts = new DiscountService(db, new CommerceUnitOfWork(db));
        var code = await discounts.CreateAsync(new("test10", "Ten percent", DiscountKind.Percentage, 10, 0, null, null, 1, true), default);
        Assert.Equal("TEST10", code.Code);

        var first = Ordering(firstScope, clock);
        var second = Ordering(secondScope, clock);
        var cart1 = await first.SaveCartAsync(new(null, [new(variant, 1)]), default);
        cart1 = await first.SetDiscountAsync(new(cart1.Version, "test10"), default);
        Assert.Equal(90, cart1.Total);
        var initialRequest = Checkout(cart1);
        var firstOrder = await first.CheckoutAsync(initialRequest, default);
        Assert.Equal(10, firstOrder.DiscountAmount);
        Assert.Equal(10, Assert.Single(firstOrder.Items).DiscountAmount);
        Assert.Equal(firstOrder.Id, (await first.CheckoutAsync(initialRequest, default)).Id);
        Assert.Equal(1, (await discounts.GetAsync(code.Id, default)).UsedCount);

        var cart2 = await second.SaveCartAsync(new(null, [new(variant, 1)]), default);
        cart2 = await second.SetDiscountAsync(new(cart2.Version, "TEST10"), default);
        Assert.NotNull(cart2.DiscountError);
        Assert.False(cart2.CanCheckout);
        clock.Current = clock.Current.AddMinutes(2);
        Assert.Equal(1, await first.ExpireAsync(default));
        cart2 = await second.GetCartAsync(default);
        Assert.Null(cart2.DiscountError);
        var secondOrder = await second.CheckoutAsync(Checkout(cart2), default);

        var expired = await first.GetOrderAsync(firstOrder.Id, false, default);
        var paid = await first.ConfirmPaymentAsync(firstOrder.Id,
            new(expired.Version, Guid.NewGuid(), "LATE-DISCOUNT", firstOrder.Total, "VND", clock.Current.UtcDateTime), default);
        Assert.Equal(OrderPaymentStatus.Paid, paid.PaymentStatus);
        Assert.Equal(OrderFulfillmentStatus.NeedsReview, paid.FulfillmentStatus);
        Assert.Equal(1, (await discounts.GetAsync(code.Id, default)).UsedCount);
        await Assert.ThrowsAsync<ConflictException>(() => first.RetryFulfillmentAsync(paid.Id, new(paid.Version), default));
        await second.CancelAsync(secondOrder.Id, new(secondOrder.Version), false, default);
        Assert.Equal(0, (await discounts.GetAsync(code.Id, default)).UsedCount);
        var resumed = await first.RetryFulfillmentAsync(paid.Id, new(paid.Version), default);
        Assert.NotEqual(OrderFulfillmentStatus.NeedsReview, resumed.FulfillmentStatus);
        Assert.Equal(1, (await discounts.GetAsync(code.Id, default)).UsedCount);
        Assert.Equal(1, await db.PaymentReceipts.AsNoTracking().CountAsync());

        // Both carts receive a valid quote before either checkout attempts the last usage.
        var raceCode = await discounts.CreateAsync(new("RACE10", "Final usage", DiscountKind.Percentage,
            10, 0, null, null, 1, true), default);
        using var raceScopeA = fixture.Scope(Guid.NewGuid());
        using var raceScopeB = fixture.Scope(Guid.NewGuid());
        var raceA = Ordering(raceScopeA, clock);
        var raceB = Ordering(raceScopeB, clock);
        var raceCartA = await raceA.SaveCartAsync(new(null, [new(variant, 1)]), default);
        var raceCartB = await raceB.SaveCartAsync(new(null, [new(variant, 1)]), default);
        raceCartA = await raceA.SetDiscountAsync(new(raceCartA.Version, raceCode.Code), default);
        raceCartB = await raceB.SetDiscountAsync(new(raceCartB.Version, raceCode.Code), default);
        Assert.Null(raceCartA.DiscountError);
        Assert.Null(raceCartB.DiscountError);
        var raceRequestA = Checkout(raceCartA);
        var raceRequestB = Checkout(raceCartB);
        var attempts = await Task.WhenAll(
            Record.ExceptionAsync(async () => await raceA.CheckoutAsync(raceRequestA, default)),
            Record.ExceptionAsync(async () => await raceB.CheckoutAsync(raceRequestB, default)));
        Assert.Single(attempts, failure => failure is null);
        Assert.IsType<ConflictException>(Assert.Single(attempts, failure => failure is not null));
        Assert.Equal(1, (await discounts.GetAsync(raceCode.Id, default)).UsedCount);
    }
}
