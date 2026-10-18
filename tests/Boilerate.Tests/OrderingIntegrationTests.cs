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
public sealed class OrderingIntegrationTests
{
    private static CommerceService Catalog(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = Substitute.For<IUserService>();
        users.HasPermissionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        return new(db, new CommerceUnitOfWork(db), scope.ServiceProvider.GetRequiredService<ICurrentUser>(), users,
            Options.Create(new CommerceSettings()), scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>());
    }

    private static OrderingService Ordering(IServiceScope scope, TimeProvider? clock = null,
        OrderingSettings? settings = null, string? deniedPermission = null)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = Substitute.For<IUserService>();
        users.HasPermissionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => (string)call[1] != deniedPermission);
        return new(db, new CommerceUnitOfWork(db), scope.ServiceProvider.GetRequiredService<ICurrentUser>(), users,
            Options.Create(settings ?? new OrderingSettings()), clock ?? TimeProvider.System,
            scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>());
    }

    private static CreateProduct Simple(ProductType type, string slug, ProductFulfillmentKind kind = ProductFulfillmentKind.Physical) =>
        new(type.Id, type.Version, new("Product " + slug, slug, null, [], [], []), [], [new(slug, 100, null, new())], kind);

    private static async Task<ProductDto> Published(CommerceService catalog, ProductType type, string slug,
        ProductFulfillmentKind kind = ProductFulfillmentKind.Physical)
    {
        var product = await catalog.CreateAsync(Simple(type, slug, kind), default);
        return await catalog.StatusAsync(new(product.Id, product.Version, ProductStatus.Published), default);
    }

    private static async Task Receive(CommerceService catalog, Guid variantId, int quantity)
    {
        var balance = (await catalog.InventoryAsync(1, 100, default)).Items.Single(i => i.VariantId == variantId);
        await catalog.AdjustAsync(new(balance.Id, balance.Version, Guid.NewGuid(), quantity, "Initial receipt"), default);
    }

    private static Checkout Request(CartDto cart, decimal total, OrderPaymentMethod method = OrderPaymentMethod.Prepaid) =>
        new(cart.Version!, Guid.NewGuid(), method, "Test Customer", "customer@example.test", "0901234567", "123 Test Street", total);

    private sealed class TestClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task AnOversizedTotalKeepsTheCartReadableAndEditableUntilTheCustomerReducesIt(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var catalog = Catalog(scope);
        var ordering = Ordering(scope);
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("CARTTOTAL", "Cart total limit", []), default);
        var product = await Published(catalog, type, "maximum-price-shirt");
        var variantId = product.Variants.Single().Id;
        await Receive(catalog, variantId, 2);
        var cart = await ordering.SaveCartAsync(new(null, [new(variantId, 2)]), default);
        Assert.True(cart.CanCheckout);
        const decimal maximumPrice = 9999999999999999m;
        await catalog.EditVariantAsync(new(product.Id, variantId, product.Version, maximumPrice, null, true, null), default);
        var oversized = await ordering.GetCartAsync(default);
        Assert.Equal(cart.Version, oversized.Version);
        Assert.Equal(19999999999999998m, oversized.Subtotal);
        Assert.Equal(oversized.Subtotal, oversized.Total);
        Assert.True(Assert.Single(oversized.Items).IsAvailable);
        Assert.False(oversized.CanCheckout);
        var reduced = await ordering.SaveCartAsync(new(oversized.Version, [new(variantId, 1)]), default);
        Assert.NotEqual(oversized.Version, reduced.Version);
        Assert.Equal(maximumPrice, reduced.Total);
        Assert.True(reduced.CanCheckout);
        // Saving a new oversized quantity must also return the committed version, so it can be edited again.
        var expanded = await ordering.SaveCartAsync(new(reduced.Version, [new(variantId, 2)]), default);
        Assert.False(expanded.CanCheckout);
        Assert.Equal(19999999999999998m, expanded.Total);
        Assert.Equal(expanded.Version, (await ordering.GetCartAsync(default)).Version);
        var cleared = await ordering.SaveCartAsync(new(expanded.Version, []), default);
        Assert.Empty(cleared.Items);
        Assert.NotEqual(expanded.Version, cleared.Version);
        Assert.Equal(0, cleared.Total);
        Assert.False(cleared.CanCheckout);
        Assert.Equal(cleared.Version, (await ordering.GetCartAsync(default)).Version);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task UpdatingCartQuantityQuotesShippingAndPriceChangesRequireAcceptingTheNewTotal(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalog = Catalog(scope);
        var ordering = Ordering(scope, settings: new OrderingSettings { FlatShippingAmount = 25000 });
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("SHIPQUOTE", "Shipping quote", []), default);
        var product = await Published(catalog, type, "shipping-quote-shirt");
        var variantId = product.Variants.Single().Id;
        await Receive(catalog, variantId, 5);
        var cart = await ordering.SaveCartAsync(new(null, [new(variantId, 1)]), default);
        Assert.Equal(25100, cart.Total);
        cart = await ordering.SaveCartAsync(new(cart.Version, [new(variantId, 2)]), default);
        Assert.Equal(2, Assert.Single(cart.Items).Quantity);
        Assert.Equal(200, cart.Subtotal);
        Assert.Equal(25000, cart.ShippingAmount);
        Assert.Equal(25200, cart.Total);
        var persisted = await ordering.GetCartAsync(default);
        Assert.Equal(cart.Version, persisted.Version);
        Assert.Equal(25200, persisted.Total);
        Assert.Equal(2, Assert.Single(await db.Set<CartItem>().AsNoTracking().ToListAsync()).Quantity);
        await catalog.EditVariantAsync(new(product.Id, variantId, product.Version, 200, null, true, null), default);
        await Assert.ThrowsAsync<ConflictException>(() => ordering.CheckoutAsync(Request(cart, cart.Total), default));
        Assert.Empty(await db.Set<Order>().ToListAsync());
        Assert.Empty(await db.Set<PhysicalStockReservation>().ToListAsync());
        Assert.Equal(0, (await db.InventoryItems.AsNoTracking().SingleAsync()).Reserved);
        var currentQuote = await ordering.GetCartAsync(default);
        Assert.Equal(25400, currentQuote.Total);
        var order = await ordering.CheckoutAsync(Request(currentQuote, currentQuote.Total), default);
        Assert.Equal(400, order.Subtotal);
        Assert.Equal(25000, order.ShippingAmount);
        Assert.Equal(25400, order.Total);
        Assert.Equal(200, Assert.Single(order.Items).UnitPrice);
        Assert.Equal(2, (await db.InventoryItems.AsNoTracking().SingleAsync()).Reserved);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ConcurrentExpiryWorkersReleaseOverlappingOrdersAgainstTheSameBalanceOnlyOnce(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalog = Catalog(scope);
        var clock = new TestClock(new DateTimeOffset(2026, 10, 18, 10, 0, 0, TimeSpan.Zero));
        var ordering = Ordering(scope, clock, new OrderingSettings { ReservationMinutes = 7, ExpirationBatchSize = 10 });
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("BATCHEXPIRY", "Batch expiry", []), default);
        var physical = await Published(catalog, type, "batch-shirt");
        var unique = await Published(catalog, type, "batch-code", ProductFulfillmentKind.DigitalUnique);
        var physicalId = physical.Variants.Single().Id;
        var uniqueId = unique.Variants.Single().Id;
        await Receive(catalog, physicalId, 2);
        await catalog.ImportDigitalAsync(new(unique.Id, uniqueId, unique.Version, [new("ONE", "BatchSecret1"), new("TWO", "BatchSecret2")]), default);
        var cart = await ordering.SaveCartAsync(new(null, [new(physicalId, 1), new(uniqueId, 1)]), default);
        var first = await ordering.CheckoutAsync(Request(cart, 200), default);
        var cleared = await ordering.GetCartAsync(default);
        cart = await ordering.SaveCartAsync(new(cleared.Version, [new(physicalId, 1), new(uniqueId, 1)]), default);
        var second = await ordering.CheckoutAsync(Request(cart, 200), default);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, (await db.InventoryItems.AsNoTracking().SingleAsync()).Reserved);
        Assert.Equal(2, await db.DigitalStockItems.CountAsync(i => i.State == DigitalStockState.Reserved));
        clock.Advance(TimeSpan.FromMinutes(8));
        using var expiryTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        async Task<int> Expire()
        {
            using var workerScope = fixture.Scope();
            return await Ordering(workerScope, clock).ExpireAsync(expiryTimeout.Token);
        }
        var expiredByWorker = await Task.WhenAll(Expire(), Expire());
        Assert.Equal(2, expiredByWorker.Sum());
        Assert.Equal(0, await ordering.ExpireAsync(default));
        var balance = await db.InventoryItems.AsNoTracking().SingleAsync();
        Assert.Equal(2, balance.OnHand);
        Assert.Equal(0, balance.Reserved);
        Assert.Equal(2, await db.Set<Order>().CountAsync(o => o.Status == OrderStatus.Expired));
        Assert.Equal(2, await db.Set<PhysicalStockReservation>().CountAsync(r => r.State == HoldState.Released));
        Assert.All(await db.DigitalStockItems.AsNoTracking().ToListAsync(), row =>
        {
            Assert.Equal(DigitalStockState.Available, row.State);
            Assert.Null(row.ReservedForOrderItemId);
        });
        Assert.Single(await db.StockMovements.AsNoTracking().ToListAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ConcurrentCheckoutAndPhysicalAdjustmentRespectTheSameLockOrderWithoutDeadlock(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var customer = Guid.NewGuid();
        Checkout checkout;
        InventoryDto initialBalance;
        using (var scope = fixture.Scope(customer))
        {
            var catalog = Catalog(scope);
            await catalog.InitializeAsync(default);
            var type = await catalog.CreateTypeAsync(new("LOCKORDER", "Lock ordering", []), default);
            var physical = await Published(catalog, type, "simultaneous-stock-adjustment");
            var variantId = physical.Variants.Single().Id;
            await Receive(catalog, variantId, 2);
            initialBalance = Assert.Single((await catalog.InventoryAsync(1, 20, default)).Items);
            var cart = await Ordering(scope).SaveCartAsync(new(null, [new(variantId, 1)]), default);
            checkout = Request(cart, 100);
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<OrderDto> PerformCheckout()
        {
            using var scope = fixture.Scope(customer);
            await start.Task;
            return await Ordering(scope).CheckoutAsync(checkout, timeout.Token);
        }
        async Task<bool> Adjust()
        {
            using var scope = fixture.Scope(Guid.NewGuid());
            await start.Task;
            try
            {
                await Catalog(scope).AdjustAsync(new(initialBalance.Id, initialBalance.Version, Guid.NewGuid(), -1, "Concurrent stock adjustment"), timeout.Token);
                return true;
            }
            catch (ConflictException) { return false; }
        }
        var checkoutTask = PerformCheckout();
        var adjustmentTask = Adjust();
        start.SetResult();
        await Task.WhenAll(checkoutTask, adjustmentTask);
        Assert.Equal(OrderStatus.PendingPayment, checkoutTask.Result.Status);
        using var verificationScope = fixture.Scope(customer);
        var db = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var balance = await db.InventoryItems.AsNoTracking().SingleAsync();
        Assert.Equal(1, balance.Reserved);
        Assert.Equal(adjustmentTask.Result ? 1 : 2, balance.OnHand);
        Assert.Single(await db.Set<Order>().ToListAsync());
        Assert.Single(await db.Set<PhysicalStockReservation>().Where(r => r.State == HoldState.Held).ToListAsync());
        Assert.Equal(adjustmentTask.Result ? 2 : 1, await db.StockMovements.CountAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task MixedCheckoutReservesEachStockSourceAndKeepsImmutableSecretFreeSnapshots(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var customer = Guid.NewGuid();
        using var scope = fixture.Scope(customer);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalog = Catalog(scope);
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("FLEXORDER", "Flexible products", []), default);
        var physical = await catalog.CreateAsync(Simple(type, "shirt") with
        {
            Options = [new("color", "Color", [new("black", "Black")])],
            Variants = [new("SHIRT-BLACK", 100, null, new() { ["color"] = "black" })]
        }, default);
        physical = await catalog.StatusAsync(new(physical.Id, physical.Version, ProductStatus.Published), default);
        var unique = await Published(catalog, type, "activation", ProductFulfillmentKind.DigitalUnique);
        var shared = await Published(catalog, type, "course", ProductFulfillmentKind.DigitalShared);
        var extension = await Published(catalog, type, "renewal", ProductFulfillmentKind.ManualExtension);
        await Receive(catalog, physical.Variants.Single().Id, 3);
        await catalog.ImportDigitalAsync(new(unique.Id, unique.Variants.Single().Id, unique.Version,
            [new("A", "ActivationSecret-A"), new("B", "ActivationSecret-B"), new("C", "ActivationSecret-C")]), default);
        await catalog.SaveSharedAsync(new(shared.Id, shared.Variants.Single().Id, shared.Version, "https://private.example.test/course-secret"), default);

        var ordering = Ordering(scope, settings: new OrderingSettings { FlatShippingAmount = 40 });
        var empty = await ordering.GetCartAsync(default);
        Assert.Equal(Guid.Empty, empty.Id);
        Assert.Null(empty.Version);
        var cart = await ordering.SaveCartAsync(new(null,
            [new(physical.Variants.Single().Id, 2), new(unique.Variants.Single().Id, 2),
                new(shared.Variants.Single().Id, 2), new(extension.Variants.Single().Id, 2)]), default);
        Assert.Equal(800, cart.Subtotal);
        Assert.All(cart.Items, item => Assert.True(item.IsAvailable));
        Assert.Equal(0, (await db.InventoryItems.AsNoTracking().SingleAsync()).Reserved);
        Assert.Equal(4, await db.DigitalStockItems.CountAsync(i => i.State == DigitalStockState.Available));

        var order = await ordering.CheckoutAsync(Request(cart, 840), default);
        Assert.Equal(OrderStatus.PendingPayment, order.Status);
        Assert.Equal(800, order.Subtotal);
        Assert.Equal(40, order.ShippingAmount);
        Assert.Equal(840, order.Total);
        Assert.Equal("VND", order.Currency);
        Assert.Equal(4, order.Items.Count);
        Assert.NotNull(order.ExpiresAt);
        var shirt = order.Items.Single(i => i.ProductId == physical.Id);
        Assert.Equal("SHIRT-BLACK", shirt.Sku);
        Assert.Contains("Black", shirt.OptionDescription);
        Assert.Equal(ProductFulfillmentKind.DigitalShared, order.Items.Single(i => i.ProductId == shared.Id).FulfillmentKind);
        Assert.Empty((await ordering.GetCartAsync(default)).Items);
        var balance = await db.InventoryItems.AsNoTracking().SingleAsync();
        Assert.Equal(3, balance.OnHand);
        Assert.Equal(2, balance.Reserved);
        var digital = await db.DigitalStockItems.AsNoTracking().ToListAsync();
        var held = digital.Where(i => i.State == DigitalStockState.Reserved).ToList();
        Assert.Equal(2, held.Count);
        Assert.All(held, row => Assert.Equal(order.Items.Single(i => i.ProductId == unique.Id).Id, row.ReservedForOrderItemId));
        Assert.Single(digital.Where(i => i.FulfillmentKind == ProductFulfillmentKind.DigitalShared && i.State == DigitalStockState.Available));
        Assert.Single(digital.Where(i => i.FulfillmentKind == ProductFulfillmentKind.DigitalUnique && i.State == DigitalStockState.Available));
        var json = JsonSerializer.Serialize(order);
        Assert.DoesNotContain("ActivationSecret", json);
        Assert.DoesNotContain("course-secret", json);
        Assert.DoesNotContain("ProtectedContent", json);
        Assert.All(digital, row => Assert.DoesNotContain(row.ProtectedContent, json));

        var current = await catalog.GetAsync(physical.Id, false, default);
        current = await catalog.EditAsync(new(current.Id, current.Version, type.Version,
            new("Renamed product", current.Slug, null, [], [], [])), default);
        await catalog.EditVariantAsync(new(current.Id, current.Variants.Single().Id, current.Version, 500, null, true, null), default);
        var snapshot = await ordering.GetOrderAsync(order.Id, false, default);
        Assert.Equal("Product shirt", snapshot.Items.Single(i => i.ProductId == physical.Id).Name);
        Assert.Equal(100, snapshot.Items.Single(i => i.ProductId == physical.Id).UnitPrice);
        Assert.Equal(840, snapshot.Total);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task DigitalCodAndMissingOneLineRollbackTheWholeCheckout(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var catalog = Catalog(scope);
        var ordering = Ordering(scope);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("ROLLBACK", "Rollback", []), default);
        var physical = await Published(catalog, type, "available-shirt");
        var unique = await Published(catalog, type, "missing-code", ProductFulfillmentKind.DigitalUnique);
        await Receive(catalog, physical.Variants.Single().Id, 1);
        var cart = await ordering.SaveCartAsync(new(null, [new(physical.Variants.Single().Id, 1), new(unique.Variants.Single().Id, 1)]), default);
        Assert.False(cart.Items.Single(i => i.VariantId == unique.Variants.Single().Id).IsAvailable);
        await Assert.ThrowsAsync<DomainException>(() => ordering.CheckoutAsync(Request(cart, 200, OrderPaymentMethod.CashOnDelivery), default));
        await Assert.ThrowsAsync<ConflictException>(() => ordering.CheckoutAsync(Request(cart, 200), default));
        Assert.Equal(0, await db.Set<Order>().CountAsync());
        Assert.Equal(0, (await db.InventoryItems.AsNoTracking().SingleAsync()).Reserved);
        Assert.Equal(1, (await db.InventoryItems.AsNoTracking().SingleAsync()).OnHand);
        Assert.Equal(2, (await ordering.GetCartAsync(default)).Items.Count);
        Assert.Equal(cart.Version, (await ordering.GetCartAsync(default)).Version);
        Assert.Empty(await db.Set<PhysicalStockReservation>().ToListAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task CheckoutRequiresCurrentCartAndExplicitAcceptanceOfTheCurrentPrice(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var catalog = Catalog(scope);
        var ordering = Ordering(scope);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("PRICING", "Pricing", []), default);
        var product = await Published(catalog, type, "priced-shirt");
        var variant = product.Variants.Single();
        await Receive(catalog, variant.Id, 5);
        var first = await ordering.SaveCartAsync(new(null, [new(variant.Id, 1)]), default);
        var current = await ordering.SaveCartAsync(new(first.Version, [new(variant.Id, 2)]), default);
        await Assert.ThrowsAsync<ConflictException>(() => ordering.SaveCartAsync(new(first.Version, []), default));
        await Assert.ThrowsAsync<ConflictException>(() => ordering.CheckoutAsync(Request(first, 100), default));
        await catalog.EditVariantAsync(new(product.Id, variant.Id, product.Version, 200, null, true, null), default);
        await Assert.ThrowsAsync<ConflictException>(() => ordering.CheckoutAsync(Request(current, 200), default));
        Assert.Equal(0, await db.Set<Order>().CountAsync());
        Assert.Equal(0, (await db.InventoryItems.AsNoTracking().SingleAsync()).Reserved);
        var refreshed = await ordering.GetCartAsync(default);
        Assert.Equal(400, refreshed.Subtotal);
        var order = await ordering.CheckoutAsync(Request(refreshed, 400), default);
        Assert.Equal(400, order.Total);
        Assert.Equal(200, Assert.Single(order.Items).UnitPrice);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task CheckoutRetryReturnsTheOriginalOrderAndRejectsChangedPayload(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var catalog = Catalog(scope);
        var ordering = Ordering(scope);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("RETRY", "Retry", []), default);
        var product = await Published(catalog, type, "idempotent-code", ProductFulfillmentKind.DigitalUnique);
        await catalog.ImportDigitalAsync(new(product.Id, product.Variants.Single().Id, product.Version, [new("ONLY", "UniqueSecret")]), default);
        var cart = await ordering.SaveCartAsync(new(null, [new(product.Variants.Single().Id, 1)]), default);
        var request = Request(cart, 100);
        var first = await ordering.CheckoutAsync(request, default);
        var replay = await ordering.CheckoutAsync(request, default);
        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(first.Version, replay.Version);
        Assert.Single(await db.Set<Order>().ToListAsync());
        Assert.Single(await db.DigitalStockItems.Where(i => i.State == DigitalStockState.Reserved).ToListAsync());
        await Assert.ThrowsAsync<ConflictException>(() => ordering.CheckoutAsync(request with { Email = "different@example.test" }, default));
        var cancelled = await ordering.CancelAsync(first.Id, new(first.Version), false, default);
        var afterCancelReplay = await ordering.CheckoutAsync(request, default);
        Assert.Equal(cancelled.Id, afterCancelReplay.Id);
        Assert.Equal(OrderStatus.Cancelled, afterCancelReplay.Status);
        Assert.Single(await db.Set<Order>().ToListAsync());
        Assert.Equal(DigitalStockState.Available, (await db.DigitalStockItems.AsNoTracking().SingleAsync()).State);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ConcurrentCustomersCannotReserveTheLastPhysicalAndUniqueItemsTwice(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        Guid physicalId;
        Guid uniqueId;
        using (var scope = fixture.Scope(Guid.NewGuid()))
        {
            var catalog = Catalog(scope);
            await catalog.InitializeAsync(default);
            var type = await catalog.CreateTypeAsync(new("LAST", "Last items", []), default);
            var physical = await Published(catalog, type, "last-shirt");
            var unique = await Published(catalog, type, "last-code", ProductFulfillmentKind.DigitalUnique);
            physicalId = physical.Variants.Single().Id;
            uniqueId = unique.Variants.Single().Id;
            await Receive(catalog, physicalId, 1);
            await catalog.ImportDigitalAsync(new(unique.Id, uniqueId, unique.Version, [new("ONLY", "LastUniqueSecret")]), default);
        }
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();
        async Task<Checkout> Prepare(Guid customer)
        {
            using var scope = fixture.Scope(customer);
            var cart = await Ordering(scope).SaveCartAsync(new(null, [new(physicalId, 1), new(uniqueId, 1)]), default);
            return Request(cart, 200);
        }
        var first = await Prepare(customerA);
        var second = await Prepare(customerB);
        async Task<bool> Compete(Guid customer, Checkout request)
        {
            using var scope = fixture.Scope(customer);
            try { await Ordering(scope).CheckoutAsync(request, default); return true; }
            catch (ConflictException) { return false; }
        }
        var results = await Task.WhenAll(Compete(customerA, first), Compete(customerB, second));
        Assert.Single(results.Where(success => success));
        using (var scope = fixture.Scope(Guid.NewGuid()))
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Single(await db.Set<Order>().ToListAsync());
            var balance = await db.InventoryItems.AsNoTracking().SingleAsync();
            Assert.Equal(1, balance.OnHand);
            Assert.Equal(1, balance.Reserved);
            Assert.Single(await db.DigitalStockItems.Where(i => i.State == DigitalStockState.Reserved).ToListAsync());
            Assert.Single(await db.Set<PhysicalStockReservation>().ToListAsync());
        }
        using var losingScope = fixture.Scope(results[0] ? customerB : customerA);
        Assert.Equal(2, (await Ordering(losingScope).GetCartAsync(default)).Items.Count);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ConfiguredExpiryAndCancellationReleaseHoldsOnlyOnceAndCodHasNoPaymentTtl(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var catalog = Catalog(scope);
        var clock = new TestClock(new DateTimeOffset(2026, 10, 18, 10, 0, 0, TimeSpan.Zero));
        var ordering = Ordering(scope, clock, new OrderingSettings { ReservationMinutes = 7 });
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("EXPIRY", "Expiry", []), default);
        var physical = await Published(catalog, type, "expiring-shirt");
        var unique = await Published(catalog, type, "expiring-code", ProductFulfillmentKind.DigitalUnique);
        var physicalId = physical.Variants.Single().Id;
        var uniqueId = unique.Variants.Single().Id;
        await Receive(catalog, physicalId, 3);
        await catalog.ImportDigitalAsync(new(unique.Id, uniqueId, unique.Version, [new("CODE", "ExpirySecret")]), default);
        var cart = await ordering.SaveCartAsync(new(null, [new(physicalId, 1), new(uniqueId, 1)]), default);
        var prepaid = await ordering.CheckoutAsync(Request(cart, 200), default);
        Assert.Equal(clock.GetUtcNow().UtcDateTime.AddMinutes(7), prepaid.ExpiresAt);
        clock.Advance(TimeSpan.FromMinutes(6));
        Assert.Equal(0, await ordering.ExpireAsync(default));
        Assert.Equal(1, (await db.InventoryItems.AsNoTracking().SingleAsync()).Reserved);
        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(1, await ordering.ExpireAsync(default));
        Assert.Equal(0, await ordering.ExpireAsync(default));
        Assert.Equal(OrderStatus.Expired, (await ordering.GetOrderAsync(prepaid.Id, false, default)).Status);
        Assert.Equal(0, (await db.InventoryItems.AsNoTracking().SingleAsync()).Reserved);
        Assert.Equal(DigitalStockState.Available, (await db.DigitalStockItems.AsNoTracking().SingleAsync()).State);
        Assert.Null((await db.DigitalStockItems.AsNoTracking().SingleAsync()).ReservedForOrderItemId);

        cart = await ordering.GetCartAsync(default);
        cart = await ordering.SaveCartAsync(new(cart.Version, [new(physicalId, 1)]), default);
        var cod = await ordering.CheckoutAsync(Request(cart, 100, OrderPaymentMethod.CashOnDelivery), default);
        Assert.Equal(OrderStatus.AwaitingProcessing, cod.Status);
        Assert.Null(cod.ExpiresAt);
        clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(0, await ordering.ExpireAsync(default));
        Assert.Equal(1, (await db.InventoryItems.AsNoTracking().SingleAsync()).Reserved);
        var cancelled = await ordering.CancelAsync(cod.Id, new(cod.Version), false, default);
        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        await Assert.ThrowsAsync<ConflictException>(() => ordering.CancelAsync(cod.Id, new(cod.Version), false, default));
        var again = await ordering.CancelAsync(cod.Id, new(cancelled.Version), false, default);
        Assert.Equal(cancelled.Version, again.Version);
        Assert.Equal(0, (await db.InventoryItems.AsNoTracking().SingleAsync()).Reserved);
        Assert.Equal(3, (await db.InventoryItems.AsNoTracking().SingleAsync()).OnHand);
        Assert.Equal(1, await db.StockMovements.CountAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task OwnershipAndIndependentAdminPermissionsProtectOrdersAndReservedInventory(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var owner = Guid.NewGuid();
        using var scope = fixture.Scope(owner);
        var catalog = Catalog(scope);
        var ordering = Ordering(scope);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("OWNERSHIP", "Ownership", []), default);
        var physical = await Published(catalog, type, "held-shirt");
        var unique = await Published(catalog, type, "held-code", ProductFulfillmentKind.DigitalUnique);
        await Receive(catalog, physical.Variants.Single().Id, 1);
        var digital = Assert.Single(await catalog.ImportDigitalAsync(new(unique.Id, unique.Variants.Single().Id, unique.Version, [new("HELD", "ReservedSecret")]), default));
        var cart = await ordering.SaveCartAsync(new(null, [new(physical.Variants.Single().Id, 1), new(unique.Variants.Single().Id, 1)]), default);
        var order = await ordering.CheckoutAsync(Request(cart, 200), default);
        var balance = (await catalog.InventoryAsync(1, 20, default)).Items.Single();
        await Assert.ThrowsAsync<DomainException>(() => catalog.AdjustAsync(new(balance.Id, balance.Version, Guid.NewGuid(), -1, "Cannot remove held stock"), default));
        var product = await catalog.GetAsync(unique.Id, false, default);
        await Assert.ThrowsAsync<DomainException>(() => catalog.SetDigitalStockAvailabilityAsync(new(unique.Id, unique.Variants.Single().Id, digital.Id, product.Version, false), default));
        Assert.False((await catalog.GetBySlugAsync(physical.Slug, default)).Variants.Single().InStock);
        Assert.False((await catalog.GetBySlugAsync(unique.Slug, default)).Variants.Single().InStock);
        async Task Reject(FormattableString sql)
        {
            var error = await Record.ExceptionAsync(() => db.Database.ExecuteSqlInterpolatedAsync(sql));
            Assert.True(error is Microsoft.Data.SqlClient.SqlException { Number: 547 or 2601 or 2627 } ||
                error is Npgsql.PostgresException { SqlState: "23503" or "23505" or "23514" }, error?.ToString() ?? "Constraint was not enforced.");
        }
        var physicalOrderItem = order.Items.Single(i => i.ProductId == physical.Id);
        var reservation = await db.Set<PhysicalStockReservation>().AsNoTracking().SingleAsync();
        await Reject($"UPDATE \"Inventory\".\"InventoryItems\" SET \"Reserved\"={2} WHERE \"Id\"={balance.Id}");
        await Reject($"UPDATE \"Ordering\".\"PhysicalStockReservations\" SET \"VariantId\"={unique.Variants.Single().Id} WHERE \"Id\"={reservation.Id}");
        await Reject($"UPDATE \"DigitalInventory\".\"DigitalStockItems\" SET \"ReservedForOrderItemId\"={physicalOrderItem.Id} WHERE \"Id\"={digital.Id}");
        await Reject($"UPDATE \"DigitalInventory\".\"DigitalStockItems\" SET \"State\"={1} WHERE \"Id\"={digital.Id}");
        await Reject($"DELETE FROM \"Catalog\".\"ProductVariants\" WHERE \"Id\"={physical.Variants.Single().Id}");

        using var otherScope = fixture.Scope(Guid.NewGuid());
        var other = Ordering(otherScope);
        await Assert.ThrowsAsync<NotFoundException>(() => other.GetOrderAsync(order.Id, false, default));
        await Assert.ThrowsAsync<NotFoundException>(() => other.CancelAsync(order.Id, new(order.Version), false, default));
        Assert.Equal(0, (await other.OrdersAsync(1, 20, false, default)).Total);
        Assert.Equal(1, (await ordering.OrdersAsync(1, 20, false, default)).Total);
        Assert.Equal(order.Id, (await other.GetOrderAsync(order.Id, true, default)).Id);
        await Assert.ThrowsAsync<ForbiddenException>(() => Ordering(otherScope, deniedPermission: "Permissions.Order.View").GetOrderAsync(order.Id, true, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => Ordering(otherScope, deniedPermission: "Permissions.Order.View").OrdersAsync(1, 20, true, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => Ordering(otherScope, deniedPermission: "Permissions.Order.Cancel").CancelAsync(order.Id, new(order.Version), true, default));
        Assert.Equal(1, (await db.InventoryItems.AsNoTracking().SingleAsync()).Reserved);
        var cancelled = await other.CancelAsync(order.Id, new(order.Version), true, default);
        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.Equal(0, (await db.InventoryItems.AsNoTracking().SingleAsync()).Reserved);
        Assert.Equal(DigitalStockState.Available, (await db.DigitalStockItems.AsNoTracking().SingleAsync()).State);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task HttpCartAndOrdersRequireAuthenticationOwnershipAndAdminCapabilities(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var oldEmail = Environment.GetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL");
        var oldPassword = Environment.GetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD");
        try
        {
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL", "orders@example.test");
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD", "OrderingPassword!29874");
            using var bootstrap = fixture.Scope();
            await bootstrap.ServiceProvider.GetRequiredService<FoundationBootstrap>().RunAsync(["--bootstrap-admin"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL", oldEmail);
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD", oldPassword);
        }
        Guid productVariantId;
        Guid owner;
        using (var scope = fixture.Scope(Guid.NewGuid()))
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            owner = Guid.Parse(await db.Users.Where(u => u.Email == "orders@example.test").Select(u => u.Id).SingleAsync());
            var catalog = Catalog(scope);
            await catalog.InitializeAsync(default);
            var type = await catalog.CreateTypeAsync(new("HTTPORDER", "HTTP order", []), default);
            var product = await Published(catalog, type, "http-order-shirt");
            productVariantId = product.Variants.Single().Id;
            await Receive(catalog, productVariantId, 3);
        }
        OrderDto otherOrder;
        using (var scope = fixture.Scope(Guid.NewGuid()))
        {
            var ordering = Ordering(scope);
            var cart = await ordering.SaveCartAsync(new(null, [new(productVariantId, 1)]), default);
            otherOrder = await ordering.CheckoutAsync(Request(cart, 100), default);
        }
        await using var app = await TestHostApplication.StartAsync(fixture, true);
        using var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/cart")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/orders/{otherOrder.Id}")).StatusCode);
        var login = await client.PostAsJsonAsync("/api/tokens/get", new TokenRequest("orders@example.test", "OrderingPassword!29874"));
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", token.Token);
        var response = await client.PutAsJsonAsync("/api/cart", new SaveCart(null, [new(productVariantId, 1)]));
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        var mine = (await response.Content.ReadFromJsonAsync<CartDto>())!;
        response = await client.PostAsJsonAsync("/api/orders/checkout", Request(mine, 100));
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        var order = (await response.Content.ReadFromJsonAsync<OrderDto>())!;
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/orders/{otherOrder.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/orders/{otherOrder.Id}/cancel", new CancelOrder(otherOrder.Version))).StatusCode);
        response = await client.GetAsync($"/api/orders/{order.Id}");
        response.EnsureSuccessStatusCode();
        response = await client.GetAsync("/api/admin/orders");
        response.EnsureSuccessStatusCode();
        using (var scope = fixture.Scope(owner))
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var functionId = await db.Functions.Where(f => f.Code == "Order").Select(f => f.Id).SingleAsync();
            var actionIds = await db.Actions.Where(a => a.Code == "View" || a.Code == "Cancel").Select(a => a.Id).ToListAsync();
            await db.Permissions.Where(p => p.FunctionId == functionId && actionIds.Contains(p.ActionId))
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.DeletedOn, DateTime.UtcNow));
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/admin/orders/{order.Id}/cancel", new CancelOrder(order.Version))).StatusCode);
        response = await client.PostAsJsonAsync($"/api/orders/{order.Id}/cancel", new CancelOrder(order.Version));
        response.EnsureSuccessStatusCode();
        Assert.Equal(OrderStatus.Cancelled, (await response.Content.ReadFromJsonAsync<OrderDto>())!.Status);
    }
}
