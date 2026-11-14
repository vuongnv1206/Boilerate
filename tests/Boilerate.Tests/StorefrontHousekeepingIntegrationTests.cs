using Boilerate.Application.Common.Mailing;
using Boilerate.Application.Ordering.Shared;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Ordering;
using Boilerate.Infrastructure.Commerce;
using Boilerate.Infrastructure.FileStorage;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class StorefrontHousekeepingIntegrationTests
{
    private static OutboxProcessor Processor(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return new(db, new CommerceUnitOfWork(db), scope.ServiceProvider.GetRequiredService<LocalFileStorageService>(),
            Options.Create(new CommerceSettings()), NullLogger<OutboxProcessor>.Instance,
            scope.ServiceProvider.GetRequiredService<IMailService>(),
            scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>());
    }

    private static async Task<Guid> PublishedVariantAsync(IServiceScope scope, string code)
    {
        var catalog = CatalogTestClient.ForScope(scope);
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new(code, code, []), default);
        var draft = await catalog.CreateAsync(new(type.Id, type.Version, new(code, code.ToLowerInvariant(), null, [], [], []),
            [], [new($"{code}-SKU", 100, null, new())]), default);
        var product = await catalog.StatusAsync(new(draft.Id, draft.Version, ProductStatus.Published), default);
        Guid variant = product.Variants.Single().Id;
        var balance = (await catalog.InventoryAsync(1, 10, default, variantId: variant)).Items.Single();
        await catalog.AdjustAsync(new(balance.Id, balance.Version, Guid.NewGuid(), 5, "Housekeeping test stock"), default);
        return variant;
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task SweepRemovesAbandonedCartsButKeepsRecentAndOrderedOnes(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        Guid abandoned = Guid.NewGuid(), ordered = Guid.NewGuid(), recent = Guid.NewGuid();
        using var abandonedScope = fixture.Scope(abandoned);
        using var orderedScope = fixture.Scope(ordered);
        using var recentScope = fixture.Scope(recent);
        Guid variant = await PublishedVariantAsync(abandonedScope, "SWEEP");

        await OrderingTestClient.ForScope(abandonedScope).SaveCartAsync(new(null, [new(variant, 1)]), default);
        await OrderingTestClient.ForScope(recentScope).SaveCartAsync(new(null, [new(variant, 1)]), default);
        var orderedClient = OrderingTestClient.ForScope(orderedScope);
        var orderedCart = await orderedClient.SaveCartAsync(new(null, [new(variant, 1)]), default);
        await orderedClient.CheckoutAsync(new(orderedCart.Version!, Guid.NewGuid(), OrderPaymentMethod.Prepaid,
            "Buyer", "buyer@example.test", "0901234567", "Address", orderedCart.Total), default);

        var db = abandonedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        DateTime longAgo = DateTime.UtcNow.AddDays(-40);
        await db.Set<Cart>().Where(c => c.CustomerId == abandoned || c.CustomerId == ordered)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CreatedOn, longAgo).SetProperty(c => c.LastModifiedOn, longAgo));

        await Processor(abandonedScope).SweepAsync(default);

        var left = await db.Set<Cart>().AsNoTracking().Select(c => c.CustomerId).ToListAsync();
        Assert.DoesNotContain(abandoned, left);
        Assert.Contains(ordered, left);
        Assert.Contains(recent, left);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task UnknownAndSwitchedOffDiscountCodesGiveTheSameAnswer(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        Guid variant = await PublishedVariantAsync(scope, "PROBE");
        await DiscountTestClient.ForScope(scope)
            .CreateAsync(new("hidden10", "Switched off", DiscountKind.Percentage, 10, 0, null, null, null, false), default);

        var ordering = OrderingTestClient.ForScope(scope);
        var cart = await ordering.SaveCartAsync(new(null, [new(variant, 1)]), default);
        var unknown = await ordering.SetDiscountAsync(new(cart.Version, "NOSUCHCODE"), default);
        var switchedOff = await ordering.SetDiscountAsync(new(unknown.Version, "HIDDEN10"), default);

        Assert.NotNull(unknown.DiscountError);
        Assert.Equal(unknown.DiscountError, switchedOff.DiscountError);
    }
}
