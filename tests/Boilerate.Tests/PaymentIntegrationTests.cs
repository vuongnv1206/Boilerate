using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Boilerate.Application.Catalog;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Identity.Tokens;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Ordering;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.DigitalInventory;
using Boilerate.Domain.Ordering;
using Boilerate.Infrastructure.Commerce;
using Boilerate.Infrastructure.Persistence.Context;
using Boilerate.Infrastructure.Persistence.Initialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class PaymentIntegrationTests
{
    private static CommerceService Catalog(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = Substitute.For<IUserService>();
        users.HasPermissionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        return new(db, new CommerceUnitOfWork(db), scope.ServiceProvider.GetRequiredService<ICurrentUser>(), users,
            Options.Create(new CommerceSettings()), scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>());
    }

    private static OrderingService Ordering(IServiceScope scope, TimeProvider? clock = null, string? deniedPermission = null)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = Substitute.For<IUserService>();
        users.HasPermissionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => (string)call[1] != deniedPermission);
        return new(db, new CommerceUnitOfWork(db), scope.ServiceProvider.GetRequiredService<ICurrentUser>(), users,
            Options.Create(new OrderingSettings()), clock ?? TimeProvider.System,
            scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>());
    }

    private static async Task<ProductDto> Published(CommerceService catalog, ProductType type, string slug,
        ProductFulfillmentKind kind)
    {
        var product = await catalog.CreateAsync(new(type.Id, type.Version,
            new("Product " + slug, slug, null, [], [], []), [], [new(slug, 100, null, new())], kind), default);
        return await catalog.StatusAsync(new(product.Id, product.Version, ProductStatus.Published), default);
    }

    private static async Task Receive(CommerceService catalog, Guid variantId, int quantity)
    {
        var balance = (await catalog.InventoryAsync(1, 100, default)).Items.Single(i => i.VariantId == variantId);
        await catalog.AdjustAsync(new(balance.Id, balance.Version, Guid.NewGuid(), quantity, "Initial receipt"), default);
    }

    private static async Task<OrderDto> Checkout(OrderingService ordering, params (Guid VariantId, int Quantity)[] lines)
    {
        var current = await ordering.GetCartAsync(default);
        var cart = await ordering.SaveCartAsync(new(current.Version, lines.Select(i => new CartLineInput(i.VariantId, i.Quantity)).ToList()), default);
        return await ordering.CheckoutAsync(new(cart.Version!, Guid.NewGuid(), OrderPaymentMethod.Prepaid,
            "Payment customer", "customer@example.test", "0901234567", "123 Test Street", cart.Total), default);
    }

    private static ConfirmPayment Receipt(OrderDto order, string reference = "BANK-001", TimeProvider? clock = null) =>
        new(order.Version, Guid.NewGuid(), reference, order.Total, "VND", (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime.AddSeconds(-1));

    private sealed class TestClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task MixedPaymentDeliversUniqueQuantitiesAndSharedSnapshotWithoutCompletingPhysicalOrExtension(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalog = Catalog(scope);
        var ordering = Ordering(scope);
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("MIXEDPAY", "Mixed payment", []), default);
        var physical = await Published(catalog, type, "paid-shirt", ProductFulfillmentKind.Physical);
        var unique = await Published(catalog, type, "paid-code", ProductFulfillmentKind.DigitalUnique);
        var shared = await Published(catalog, type, "paid-link", ProductFulfillmentKind.DigitalShared);
        var extension = await Published(catalog, type, "paid-extension", ProductFulfillmentKind.ManualExtension);
        await Receive(catalog, physical.Variants.Single().Id, 1);
        await catalog.ImportDigitalAsync(new(unique.Id, unique.Variants.Single().Id, unique.Version,
            [new("ONE", "PaymentSecretOne"), new("TWO", "PaymentSecretTwo")]), default);
        const string link = "https://course.example.test/private/PaymentSecretLink";
        await catalog.SaveSharedAsync(new(shared.Id, shared.Variants.Single().Id, shared.Version, link), default);
        var order = await Checkout(ordering, (physical.Variants.Single().Id, 1), (unique.Variants.Single().Id, 2),
            (shared.Variants.Single().Id, 2), (extension.Variants.Single().Id, 1));
        await Assert.ThrowsAsync<ConflictException>(() => ordering.DigitalDeliveriesAsync(order.Id, default));
        var paid = await ordering.ConfirmPaymentAsync(order.Id, Receipt(order), default);
        Assert.Equal(OrderPaymentStatus.Paid, paid.PaymentStatus);
        Assert.Equal(OrderFulfillmentStatus.InProgress, paid.FulfillmentStatus);
        Assert.Equal(OrderStatus.AwaitingProcessing, paid.Status);
        Assert.NotNull(paid.PaidAtUtc);
        var deliveries = await ordering.DigitalDeliveriesAsync(order.Id, default);
        Assert.Equal(3, deliveries.Count);
        var uniqueLine = order.Items.Single(i => i.ProductId == unique.Id);
        Assert.Equal(new[] { "PaymentSecretOne", "PaymentSecretTwo" }, deliveries.Where(d => d.OrderItemId == uniqueLine.Id).Select(d => d.Content).Order());
        Assert.Equal(new[] { 1, 2 }, deliveries.Where(d => d.OrderItemId == uniqueLine.Id).Select(d => d.Sequence).Order());
        var sharedLine = order.Items.Single(i => i.ProductId == shared.Id);
        Assert.Equal(link, Assert.Single(deliveries.Where(d => d.OrderItemId == sharedLine.Id)).Content);
        Assert.Equal(1, (await db.InventoryItems.AsNoTracking().SingleAsync()).Reserved);
        Assert.Equal(HoldState.Held, (await db.PhysicalStockReservations.AsNoTracking().SingleAsync()).State);
        Assert.Equal(2, await db.DigitalStockItems.CountAsync(i => i.State == DigitalStockState.Delivered));
        Assert.All(await db.DigitalStockItems.Where(i => i.State == DigitalStockState.Delivered).AsNoTracking().ToListAsync(), i => Assert.Equal(uniqueLine.Id, i.ReservedForOrderItemId));
        shared = await catalog.GetAsync(shared.Id, false, default);
        await catalog.SaveSharedAsync(new(shared.Id, shared.Variants.Single().Id, shared.Version, "https://course.example.test/replacement"), default);
        Assert.Equal(link, (await ordering.DigitalDeliveriesAsync(order.Id, default)).Single(d => d.OrderItemId == sharedLine.Id).Content);
        Assert.Equal(paid.Version, (await ordering.RetryFulfillmentAsync(paid.Id, new(paid.Version), default)).Version);
        await Assert.ThrowsAsync<ConflictException>(() => ordering.CancelAsync(paid.Id, new(paid.Version), false, default));
        Assert.Equal(3, await db.DigitalDeliveries.CountAsync());
        Assert.Equal(1, (await db.InventoryItems.AsNoTracking().SingleAsync()).Reserved);
        var protectedRows = await db.DigitalDeliveries.AsNoTracking().ToListAsync();
        Assert.All(protectedRows, row => Assert.DoesNotContain("PaymentSecret", row.ProtectedContent));
        var orderJson = JsonSerializer.Serialize(await ordering.GetOrderAsync(paid.Id, false, default));
        var auditJson = JsonSerializer.Serialize(await db.Trails.AsNoTracking().ToListAsync());
        Assert.DoesNotContain("PaymentSecret", orderJson);
        Assert.DoesNotContain("PaymentSecret", auditJson);
        Assert.DoesNotContain("ProtectedContent", auditJson);
        Assert.All(protectedRows, row => Assert.DoesNotContain(row.ProtectedContent, auditJson));
        Assert.DoesNotContain("PaymentSecret", JsonSerializer.Serialize(await db.OutboxMessages.AsNoTracking().ToListAsync()));
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task PaymentReplayIgnoresOldVersionButRejectsChangedPayloadAndReusedBankReference(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalog = Catalog(scope);
        var ordering = Ordering(scope);
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("PAYRETRY", "Payment retry", []), default);
        var product = await Published(catalog, type, "payment-retry-code", ProductFulfillmentKind.DigitalUnique);
        await catalog.ImportDigitalAsync(new(product.Id, product.Variants.Single().Id, product.Version,
            [new("ONE", "ReplaySecretOne"), new("TWO", "ReplaySecretTwo")]), default);
        var order = await Checkout(ordering, (product.Variants.Single().Id, 1));
        var request = Receipt(order, "transfer-001");
        var paid = await ordering.ConfirmPaymentAsync(order.Id, request, default);
        Assert.Equal(OrderStatus.Completed, paid.Status);
        Assert.Equal(OrderFulfillmentStatus.Completed, paid.FulfillmentStatus);
        Assert.Equal(paid.Version, (await ordering.ConfirmPaymentAsync(order.Id, request with { Reference = "TRANSFER-001", Currency = "vnd" }, default)).Version);
        await Assert.ThrowsAsync<ConflictException>(() => ordering.ConfirmPaymentAsync(order.Id, request with { Amount = 101 }, default));
        await Assert.ThrowsAsync<ConflictException>(() => ordering.ConfirmPaymentAsync(order.Id, request with { Reference = "OTHER" }, default));
        await Assert.ThrowsAsync<ConflictException>(() => ordering.ConfirmPaymentAsync(order.Id, request with { OperationId = Guid.NewGuid(), Version = paid.Version }, default));
        var second = await Checkout(ordering, (product.Variants.Single().Id, 1));
        await Assert.ThrowsAsync<ConflictException>(() => ordering.ConfirmPaymentAsync(second.Id, Receipt(second, "TRANSFER-001"), default));
        await Assert.ThrowsAsync<ConflictException>(() => ordering.ConfirmPaymentAsync(second.Id, request with { Version = second.Version }, default));
        Assert.Single(await db.PaymentReceipts.AsNoTracking().ToListAsync());
        Assert.Single(await db.DigitalDeliveries.AsNoTracking().ToListAsync());
        Assert.Equal(1, await db.DigitalStockItems.CountAsync(i => i.State == DigitalStockState.Delivered));
        Assert.Equal(1, await db.DigitalStockItems.CountAsync(i => i.State == DigitalStockState.Reserved));
        Assert.Equal(OrderPaymentStatus.Unpaid, (await ordering.GetOrderAsync(second.Id, false, default)).PaymentStatus);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ConfirmationRequiresDedicatedPermissionVersionAndExactVerifiedMoney(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalog = Catalog(scope);
        var ordering = Ordering(scope);
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("PAYVALID", "Payment validation", []), default);
        var product = await Published(catalog, type, "validation-code", ProductFulfillmentKind.DigitalUnique);
        await catalog.ImportDigitalAsync(new(product.Id, product.Variants.Single().Id, product.Version, [new("ONLY", "ValidationSecret")]), default);
        var order = await Checkout(ordering, (product.Variants.Single().Id, 1));
        var request = Receipt(order);
        await Assert.ThrowsAsync<ForbiddenException>(() => Ordering(scope, deniedPermission: "Permissions.Payment.Confirm").ConfirmPaymentAsync(order.Id, request, default));
        await Assert.ThrowsAsync<ConflictException>(() => ordering.ConfirmPaymentAsync(order.Id, request with { Version = "stale" }, default));
        await Assert.ThrowsAsync<DomainException>(() => ordering.ConfirmPaymentAsync(order.Id, request with { Amount = 99 }, default));
        await Assert.ThrowsAsync<DomainException>(() => ordering.ConfirmPaymentAsync(order.Id, request with { Amount = 101 }, default));
        await Assert.ThrowsAsync<DomainException>(() => ordering.ConfirmPaymentAsync(order.Id, request with { Currency = "USD" }, default));
        await Assert.ThrowsAsync<DomainException>(() => ordering.ConfirmPaymentAsync(order.Id, request with { ReceivedAtUtc = DateTime.UtcNow.AddDays(1) }, default));
        await Assert.ThrowsAsync<DomainException>(() => ordering.ConfirmPaymentAsync(order.Id, request with { ReceivedAtUtc = DateTime.SpecifyKind(request.ReceivedAtUtc, DateTimeKind.Unspecified) }, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => Ordering(scope, deniedPermission: "Permissions.Order.Fulfill").RetryFulfillmentAsync(order.Id, new(order.Version), default));
        Assert.Empty(await db.PaymentReceipts.ToListAsync());
        Assert.Empty(await db.DigitalDeliveries.ToListAsync());
        Assert.Equal(order.Version, (await ordering.GetOrderAsync(order.Id, false, default)).Version);
        Assert.Equal(DigitalStockState.Reserved, (await db.DigitalStockItems.AsNoTracking().SingleAsync()).State);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task LatePaymentReacquiresAllStockUsingOrderSnapshotsEvenAfterCatalogArchiveAndPriceChange(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalog = Catalog(scope);
        var clock = new TestClock(new(2026, 10, 18, 10, 0, 0, TimeSpan.Zero));
        var ordering = Ordering(scope, clock);
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("LATEPAY", "Late payment", []), default);
        var physical = await Published(catalog, type, "late-shirt", ProductFulfillmentKind.Physical);
        var unique = await Published(catalog, type, "late-code", ProductFulfillmentKind.DigitalUnique);
        await Receive(catalog, physical.Variants.Single().Id, 1);
        await catalog.ImportDigitalAsync(new(unique.Id, unique.Variants.Single().Id, unique.Version, [new("ONLY", "LatePaymentSecret")]), default);
        var order = await Checkout(ordering, (physical.Variants.Single().Id, 1), (unique.Variants.Single().Id, 1));
        var originalReservation = await db.PhysicalStockReservations.AsNoTracking().SingleAsync();
        clock.Advance(TimeSpan.FromMinutes(35));
        Assert.Equal(1, await ordering.ExpireAsync(default));
        Assert.Equal(0, (await db.InventoryItems.AsNoTracking().SingleAsync()).Reserved);
        var expired = await ordering.GetOrderAsync(order.Id, false, default);
        physical = await catalog.EditVariantAsync(new(physical.Id, physical.Variants.Single().Id, physical.Version, 500, null, true, null), default);
        await catalog.StatusAsync(new(physical.Id, physical.Version, ProductStatus.Archived), default);
        var paid = await ordering.ConfirmPaymentAsync(order.Id, Receipt(expired, clock: clock), default);
        Assert.Equal(200, paid.Total);
        Assert.Equal(OrderPaymentStatus.Paid, paid.PaymentStatus);
        Assert.Equal(OrderFulfillmentStatus.InProgress, paid.FulfillmentStatus);
        Assert.Equal("LatePaymentSecret", Assert.Single(await ordering.DigitalDeliveriesAsync(order.Id, default)).Content);
        var held = await db.PhysicalStockReservations.AsNoTracking().SingleAsync();
        Assert.Equal(originalReservation.Id, held.Id);
        Assert.Equal(HoldState.Held, held.State);
        Assert.Equal(1, (await db.InventoryItems.AsNoTracking().SingleAsync()).Reserved);
        Assert.Equal(0, await ordering.ExpireAsync(default));
        Assert.Equal(DigitalStockState.Delivered, (await db.DigitalStockItems.AsNoTracking().SingleAsync()).State);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task LatePaymentWithOneMissingCodeRecordsMoneyWithoutPartialHoldsThenStaffCanRetryAfterRestock(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var owner = Guid.NewGuid();
        using var scope = fixture.Scope(owner);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalog = Catalog(scope);
        var clock = new TestClock(new(2026, 10, 18, 10, 0, 0, TimeSpan.Zero));
        var ordering = Ordering(scope, clock);
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("PAYREVIEW", "Payment review", []), default);
        var physical = await Published(catalog, type, "review-shirt", ProductFulfillmentKind.Physical);
        var unique = await Published(catalog, type, "review-code", ProductFulfillmentKind.DigitalUnique);
        await Receive(catalog, physical.Variants.Single().Id, 1);
        await catalog.ImportDigitalAsync(new(unique.Id, unique.Variants.Single().Id, unique.Version,
            [new("ONE", "ReviewSecretOne"), new("TWO", "ReviewSecretTwo")]), default);
        var order = await Checkout(ordering, (physical.Variants.Single().Id, 1), (unique.Variants.Single().Id, 2));
        clock.Advance(TimeSpan.FromMinutes(35));
        Assert.Equal(1, await ordering.ExpireAsync(default));
        using var otherScope = fixture.Scope(Guid.NewGuid());
        var other = Ordering(otherScope, clock);
        var competing = await Checkout(other, (unique.Variants.Single().Id, 1));
        var expired = await ordering.GetOrderAsync(order.Id, false, default);
        var paid = await ordering.ConfirmPaymentAsync(order.Id, Receipt(expired, clock: clock), default);
        Assert.Equal(OrderPaymentStatus.Paid, paid.PaymentStatus);
        Assert.Equal(OrderFulfillmentStatus.NeedsReview, paid.FulfillmentStatus);
        Assert.Single(await db.PaymentReceipts.AsNoTracking().ToListAsync());
        Assert.Empty(await db.DigitalDeliveries.ToListAsync());
        Assert.Equal(0, (await db.InventoryItems.AsNoTracking().SingleAsync()).Reserved);
        Assert.Equal(HoldState.Released, (await db.PhysicalStockReservations.AsNoTracking().SingleAsync()).State);
        Assert.Equal(1, await db.DigitalStockItems.CountAsync(i => i.State == DigitalStockState.Available));
        Assert.Equal(1, await db.DigitalStockItems.CountAsync(i => i.State == DigitalStockState.Reserved));
        await Assert.ThrowsAsync<ForbiddenException>(() => Ordering(scope, clock, "Permissions.Order.Fulfill").RetryFulfillmentAsync(paid.Id, new(paid.Version), default));
        unique = await catalog.GetAsync(unique.Id, false, default);
        await catalog.ImportDigitalAsync(new(unique.Id, unique.Variants.Single().Id, unique.Version, [new("THREE", "ReviewSecretThree")]), default);
        var fulfilled = await ordering.RetryFulfillmentAsync(paid.Id, new(paid.Version), default);
        Assert.Equal(OrderFulfillmentStatus.InProgress, fulfilled.FulfillmentStatus);
        Assert.Equal(2, (await ordering.DigitalDeliveriesAsync(paid.Id, default)).Count);
        Assert.Equal(1, (await db.InventoryItems.AsNoTracking().SingleAsync()).Reserved);
        Assert.Single(await db.PhysicalStockReservations.ToListAsync());
        Assert.Single(await db.PaymentReceipts.ToListAsync());
        Assert.Equal(2, await db.DigitalStockItems.CountAsync(i => i.State == DigitalStockState.Delivered));
        var otherCode = await db.DigitalStockItems.AsNoTracking().SingleAsync(i => i.State == DigitalStockState.Reserved);
        Assert.Equal(Assert.Single(competing.Items).Id, otherCode.ReservedForOrderItemId);
        Assert.Equal(fulfilled.Version, (await ordering.RetryFulfillmentAsync(fulfilled.Id, new(fulfilled.Version), default)).Version);
        Assert.Equal(2, await db.DigitalDeliveries.CountAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ReceivingMoneyForACancelledOrderNeverRevivesItOrDeliversItsReleasedCode(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalog = Catalog(scope);
        var ordering = Ordering(scope);
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("CANCELPAY", "Cancelled payment", []), default);
        var product = await Published(catalog, type, "cancelled-code", ProductFulfillmentKind.DigitalUnique);
        await catalog.ImportDigitalAsync(new(product.Id, product.Variants.Single().Id, product.Version, [new("ONLY", "CancelledPaymentSecret")]), default);
        var order = await Checkout(ordering, (product.Variants.Single().Id, 1));
        var cancelled = await ordering.CancelAsync(order.Id, new(order.Version), false, default);
        var paid = await ordering.ConfirmPaymentAsync(order.Id, Receipt(cancelled), default);
        Assert.Equal(OrderStatus.Cancelled, paid.Status);
        Assert.Equal(OrderPaymentStatus.Paid, paid.PaymentStatus);
        Assert.Equal(OrderFulfillmentStatus.NeedsReview, paid.FulfillmentStatus);
        Assert.Single(await db.PaymentReceipts.ToListAsync());
        Assert.Empty(await db.DigitalDeliveries.ToListAsync());
        Assert.Equal(DigitalStockState.Available, (await db.DigitalStockItems.AsNoTracking().SingleAsync()).State);
        await Assert.ThrowsAsync<ConflictException>(() => ordering.RetryFulfillmentAsync(paid.Id, new(paid.Version), default));
        Assert.Empty(await ordering.DigitalDeliveriesAsync(paid.Id, default));
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ConcurrentConfirmationRecordsTheReceiptAndDeliversOnlyOnce(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var owner = Guid.NewGuid();
        OrderDto order;
        using (var scope = fixture.Scope(owner))
        {
            var catalog = Catalog(scope);
            await catalog.InitializeAsync(default);
            var type = await catalog.CreateTypeAsync(new("RACEPAY", "Race payment", []), default);
            var product = await Published(catalog, type, "race-payment-code", ProductFulfillmentKind.DigitalUnique);
            await catalog.ImportDigitalAsync(new(product.Id, product.Variants.Single().Id, product.Version, [new("ONLY", "RacePaymentSecret")]), default);
            order = await Checkout(Ordering(scope), (product.Variants.Single().Id, 1));
        }
        var first = Receipt(order, "SAME-RECEIPT");
        var second = first with { OperationId = Guid.NewGuid() };
        async Task<bool> Confirm(ConfirmPayment request)
        {
            using var scope = fixture.Scope(Guid.NewGuid());
            try { await Ordering(scope).ConfirmPaymentAsync(order.Id, request, default); return true; }
            catch (ConflictException) { return false; }
        }
        var results = await Task.WhenAll(Confirm(first), Confirm(second));
        Assert.Single(results.Where(success => success));
        using var finalScope = fixture.Scope(owner);
        var db = finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Single(await db.PaymentReceipts.ToListAsync());
        Assert.Single(await db.DigitalDeliveries.ToListAsync());
        Assert.Equal(DigitalStockState.Delivered, (await db.DigitalStockItems.AsNoTracking().SingleAsync()).State);
        var paid = await Ordering(finalScope).GetOrderAsync(order.Id, false, default);
        Assert.Equal(OrderStatus.Completed, paid.Status);
        Assert.Equal("RacePaymentSecret", Assert.Single(await Ordering(finalScope).DigitalDeliveriesAsync(order.Id, default)).Content);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ConcurrentRetriesForTheLastCodeKeepBothReceiptsButDeliverToOnlyOneOrder(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var clock = new TestClock(new(2026, 10, 18, 10, 0, 0, TimeSpan.Zero));
        var firstOwner = Guid.NewGuid();
        var secondOwner = Guid.NewGuid();
        Guid variant;
        Guid productId;
        Guid stockId;
        OrderDto first;
        using (var scope = fixture.Scope(firstOwner))
        {
            var catalog = Catalog(scope);
            await catalog.InitializeAsync(default);
            var type = await catalog.CreateTypeAsync(new("RACERETRY", "Race fulfillment retry", []), default);
            var product = await Published(catalog, type, "race-retry-code", ProductFulfillmentKind.DigitalUnique);
            productId = product.Id;
            variant = product.Variants.Single().Id;
            stockId = Assert.Single(await catalog.ImportDigitalAsync(new(product.Id, variant, product.Version, [new("ONLY", "LastRetrySecret")]), default)).Id;
            var ordering = Ordering(scope, clock);
            first = await Checkout(ordering, (variant, 1));
            clock.Advance(TimeSpan.FromMinutes(31));
            Assert.Equal(1, await ordering.ExpireAsync(default));
        }
        OrderDto second;
        using (var scope = fixture.Scope(secondOwner))
        {
            var ordering = Ordering(scope, clock);
            second = await Checkout(ordering, (variant, 1));
            clock.Advance(TimeSpan.FromMinutes(31));
            Assert.Equal(1, await ordering.ExpireAsync(default));
            var catalog = Catalog(scope);
            var product = await catalog.GetAsync(productId, false, default);
            await catalog.SetDigitalStockAvailabilityAsync(new(productId, variant, stockId, product.Version, false), default);
        }
        using (var scope = fixture.Scope(firstOwner))
        {
            var ordering = Ordering(scope, clock);
            first = await ordering.GetOrderAsync(first.Id, false, default);
            first = await ordering.ConfirmPaymentAsync(first.Id, Receipt(first, "RETRY-FIRST", clock), default);
            second = await ordering.GetOrderAsync(second.Id, true, default);
            second = await ordering.ConfirmPaymentAsync(second.Id, Receipt(second, "RETRY-SECOND", clock), default);
            Assert.Equal(OrderFulfillmentStatus.NeedsReview, first.FulfillmentStatus);
            Assert.Equal(OrderFulfillmentStatus.NeedsReview, second.FulfillmentStatus);
            var catalog = Catalog(scope);
            var product = await catalog.GetAsync(productId, false, default);
            await catalog.SetDigitalStockAvailabilityAsync(new(productId, variant, stockId, product.Version, true), default);
        }
        async Task<OrderDto> Retry(OrderDto order)
        {
            using var scope = fixture.Scope(Guid.NewGuid());
            return await Ordering(scope, clock).RetryFulfillmentAsync(order.Id, new(order.Version), default);
        }
        var results = await Task.WhenAll(Retry(first), Retry(second));
        Assert.Single(results.Where(o => o.FulfillmentStatus == OrderFulfillmentStatus.Completed));
        Assert.Single(results.Where(o => o.FulfillmentStatus == OrderFulfillmentStatus.NeedsReview));
        using var finalScope = fixture.Scope(firstOwner);
        var db = finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(2, await db.PaymentReceipts.CountAsync());
        var delivery = await db.DigitalDeliveries.AsNoTracking().SingleAsync();
        Assert.Equal(results.Single(o => o.FulfillmentStatus == OrderFulfillmentStatus.Completed).Id, delivery.OrderId);
        Assert.Equal(DigitalStockState.Delivered, (await db.DigitalStockItems.AsNoTracking().SingleAsync()).State);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task DecryptionFailurePreservesReceiptWithoutDeliveryAndStaffCanRetryAfterRepair(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalog = Catalog(scope);
        var ordering = Ordering(scope);
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("PAYROLLBACK", "Payment rollback", []), default);
        var product = await Published(catalog, type, "rollback-payment-code", ProductFulfillmentKind.DigitalUnique);
        await catalog.ImportDigitalAsync(new(product.Id, product.Variants.Single().Id, product.Version, [new("ONLY", "RollbackPaymentSecret")]), default);
        var order = await Checkout(ordering, (product.Variants.Single().Id, 1));
        var before = await db.DigitalStockItems.AsNoTracking().SingleAsync();
        var outboxCount = await db.OutboxMessages.CountAsync();
        await db.DigitalStockItems.Where(i => i.Id == before.Id).ExecuteUpdateAsync(s => s.SetProperty(i => i.ProtectedContent, "invalid-ciphertext"));
        var paid = await ordering.ConfirmPaymentAsync(order.Id, Receipt(order), default);
        Assert.Single(await db.PaymentReceipts.ToListAsync());
        Assert.Empty(await db.DigitalDeliveries.ToListAsync());
        var persisted = await ordering.GetOrderAsync(order.Id, false, default);
        Assert.NotEqual(order.Version, persisted.Version);
        Assert.Equal(OrderPaymentStatus.Paid, persisted.PaymentStatus);
        Assert.Equal(OrderFulfillmentStatus.NeedsReview, persisted.FulfillmentStatus);
        var stock = await db.DigitalStockItems.AsNoTracking().SingleAsync();
        Assert.Equal(before.Version, stock.Version);
        Assert.Equal(DigitalStockState.Reserved, stock.State);
        Assert.Equal(before.ReservedForOrderItemId, stock.ReservedForOrderItemId);
        Assert.Equal(outboxCount, await db.OutboxMessages.CountAsync());
        var auditJson = JsonSerializer.Serialize(await db.Trails.AsNoTracking().ToListAsync());
        Assert.DoesNotContain("invalid-ciphertext", auditJson);
        Assert.DoesNotContain("RollbackPaymentSecret", auditJson);
        await db.DigitalStockItems.Where(i => i.Id == before.Id).ExecuteUpdateAsync(s => s.SetProperty(i => i.ProtectedContent, before.ProtectedContent));
        var fulfilled = await ordering.RetryFulfillmentAsync(paid.Id, new(paid.Version), default);
        Assert.Equal(OrderFulfillmentStatus.Completed, fulfilled.FulfillmentStatus);
        Assert.Equal("RollbackPaymentSecret", Assert.Single(await ordering.DigitalDeliveriesAsync(order.Id, default)).Content);
        Assert.Single(await db.PaymentReceipts.ToListAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task DeliveredHistoryHasImmutableRowsAndDatabaseEnforcesOrderSkuOwnershipAndUniqueStockReuse(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var owner = Guid.NewGuid();
        using var scope = fixture.Scope(owner);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalog = Catalog(scope);
        var ordering = Ordering(scope);
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("PAYCONSTRAINT", "Payment constraints", []), default);
        var product = await Published(catalog, type, "constraint-payment-code", ProductFulfillmentKind.DigitalUnique);
        await catalog.ImportDigitalAsync(new(product.Id, product.Variants.Single().Id, product.Version,
            [new("ONE", "ConstraintSecretOne"), new("TWO", "ConstraintSecretTwo")]), default);
        var first = await Checkout(ordering, (product.Variants.Single().Id, 1));
        await ordering.ConfirmPaymentAsync(first.Id, Receipt(first, "CONSTRAINT-FIRST"), default);
        var second = await Checkout(ordering, (product.Variants.Single().Id, 1));
        await ordering.ConfirmPaymentAsync(second.Id, Receipt(second, "CONSTRAINT-SECOND"), default);
        var delivered = await db.DigitalDeliveries.AsNoTracking().OrderBy(d => d.Id).ToListAsync();
        async Task Reject(FormattableString sql)
        {
            var error = await Record.ExceptionAsync(() => db.Database.ExecuteSqlInterpolatedAsync(sql));
            Assert.True(error is Microsoft.Data.SqlClient.SqlException { Number: 547 or 2601 or 2627 } ||
                error is Npgsql.PostgresException { SqlState: "23503" or "23505" or "23514" }, error?.ToString() ?? "Constraint was not enforced.");
        }
        await Reject($"UPDATE \"Ordering\".\"DigitalDeliveries\" SET \"OrderId\"={Guid.NewGuid()} WHERE \"Id\"={delivered[0].Id}");
        await Reject($"UPDATE \"Ordering\".\"DigitalDeliveries\" SET \"StockItemId\"={delivered[0].StockItemId} WHERE \"Id\"={delivered[1].Id}");
        await Reject($"UPDATE \"DigitalInventory\".\"DigitalStockItems\" SET \"ReservedForOrderItemId\"={null as Guid?} WHERE \"Id\"={delivered[0].StockItemId}");
        var firstStock = await db.DigitalStockItems.AsNoTracking().SingleAsync(i => i.Id == delivered[0].StockItemId);
        product = await catalog.GetAsync(product.Id, false, default);
        await Assert.ThrowsAsync<DomainException>(() => catalog.SetDigitalStockAvailabilityAsync(new(product.Id, product.Variants.Single().Id, firstStock.Id, product.Version, true), default));
        using (var receiptScope = fixture.Scope(owner))
        {
            var receiptDb = receiptScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            receiptDb.PaymentReceipts.Remove(await receiptDb.PaymentReceipts.FirstAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => receiptDb.SaveChangesAsync());
        }
        using (var deliveryScope = fixture.Scope(owner))
        {
            var deliveryDb = deliveryScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            deliveryDb.DigitalDeliveries.Remove(await deliveryDb.DigitalDeliveries.FirstAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => deliveryDb.SaveChangesAsync());
        }
        Assert.Equal(2, await db.PaymentReceipts.CountAsync());
        Assert.Equal(2, await db.DigitalDeliveries.CountAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task HttpPaymentAndSecretDeliveryEnforceAuthenticationDedicatedAclOwnershipAndNoStore(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var oldEmail = Environment.GetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL");
        var oldPassword = Environment.GetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD");
        try
        {
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL", "payments@example.test");
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD", "PaymentPassword!29874");
            using var bootstrap = fixture.Scope();
            await bootstrap.ServiceProvider.GetRequiredService<FoundationBootstrap>().RunAsync(["--bootstrap-admin"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL", oldEmail);
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD", oldPassword);
        }
        Guid owner;
        Guid variant;
        using (var scope = fixture.Scope(Guid.NewGuid()))
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            owner = Guid.Parse(await db.Users.Where(u => u.Email == "payments@example.test").Select(u => u.Id).SingleAsync());
            var catalog = Catalog(scope);
            await catalog.InitializeAsync(default);
            var type = await catalog.CreateTypeAsync(new("HTTPPAY", "HTTP payment", []), default);
            var product = await Published(catalog, type, "http-payment-code", ProductFulfillmentKind.DigitalUnique);
            variant = product.Variants.Single().Id;
            await catalog.ImportDigitalAsync(new(product.Id, variant, product.Version,
                [new("ONE", "HttpPaymentSecretOne"), new("TWO", "HttpPaymentSecretTwo"), new("THREE", "HttpPaymentSecretThree")]), default);
        }
        OrderDto mine;
        using (var scope = fixture.Scope(owner))
            mine = await Checkout(Ordering(scope), (variant, 1));
        OrderDto another;
        string anotherSecret;
        using (var scope = fixture.Scope(Guid.NewGuid()))
        {
            var ordering = Ordering(scope);
            another = await Checkout(ordering, (variant, 1));
            await ordering.ConfirmPaymentAsync(another.Id, Receipt(another, "HTTP-OTHER"), default);
            anotherSecret = Assert.Single(await ordering.DigitalDeliveriesAsync(another.Id, default)).Content;
        }
        await using var app = await TestHostApplication.StartAsync(fixture, true);
        using var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/orders/{mine.Id}/digital-deliveries")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync($"/api/admin/orders/{mine.Id}/payment-confirmations", Receipt(mine))).StatusCode);
        var login = await client.PostAsJsonAsync("/api/tokens/get", new TokenRequest("payments@example.test", "PaymentPassword!29874"));
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", token.Token);
        Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync($"/api/orders/{mine.Id}/digital-deliveries")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/orders/{another.Id}/digital-deliveries")).StatusCode);
        var response = await client.PostAsJsonAsync($"/api/admin/orders/{mine.Id}/payment-confirmations", Receipt(mine, "HTTP-MINE"));
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        var paid = (await response.Content.ReadFromJsonAsync<OrderDto>())!;
        Assert.Equal(OrderPaymentStatus.Paid, paid.PaymentStatus);
        Assert.DoesNotContain("HttpPaymentSecret", await response.Content.ReadAsStringAsync());
        response = await client.GetAsync($"/api/orders/{mine.Id}/digital-deliveries");
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        var mySecret = Assert.Single((await response.Content.ReadFromJsonAsync<List<DigitalDeliveryDto>>())!).Content;
        Assert.StartsWith("HttpPaymentSecret", mySecret);
        Assert.NotEqual(anotherSecret, mySecret);
        using (var scope = fixture.Scope(owner))
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var functionId = await db.Functions.Where(f => f.Code == "Payment").Select(f => f.Id).SingleAsync();
            var actionId = await db.Actions.Where(a => a.Code == "Confirm").Select(a => a.Id).SingleAsync();
            await db.Permissions.Where(p => p.FunctionId == functionId && p.ActionId == actionId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.DeletedOn, DateTime.UtcNow));
        }
        response = await client.PostAsJsonAsync($"/api/admin/orders/{mine.Id}/payment-confirmations", Receipt(paid, "HTTP-DENIED"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        response = await client.GetAsync($"/api/orders/{mine.Id}/digital-deliveries");
        response.EnsureSuccessStatusCode();
    }
}
