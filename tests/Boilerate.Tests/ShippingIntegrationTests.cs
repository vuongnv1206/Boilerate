using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Boilerate.Application.Catalog;
using Boilerate.Application.Common.Events;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Identity.Tokens;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Ordering;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.DigitalInventory;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Shipping;
using Boilerate.Infrastructure.Commerce;
using Boilerate.Infrastructure.Persistence.Context;
using Boilerate.Infrastructure.Persistence.Initialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class ShippingIntegrationTests
{
    private static CommerceService Catalog(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = Substitute.For<IUserService>();
        users.HasPermissionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        return new(db, new CommerceUnitOfWork(db), scope.ServiceProvider.GetRequiredService<ICurrentUser>(), users,
            Options.Create(new CommerceSettings()), scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>());
    }

    private static OrderingService Ordering(IServiceScope scope, string? deniedPermission = null, ApplicationDbContext? dbOverride = null,
        TimeProvider? clock = null)
    {
        var db = dbOverride ?? scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = Substitute.For<IUserService>();
        users.HasPermissionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => (string)call[1] != deniedPermission);
        return new(db, new CommerceUnitOfWork(db), scope.ServiceProvider.GetRequiredService<ICurrentUser>(), users,
            Options.Create(new OrderingSettings()), clock ?? TimeProvider.System,
            scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>());
    }

    private static async Task<ProductDto> Published(CommerceService catalog, ProductType type, string slug,
        ProductFulfillmentKind kind = ProductFulfillmentKind.Physical)
    {
        var product = await catalog.CreateAsync(new(type.Id, type.Version,
            new("Product " + slug, slug, null, [], [], []), [], [new(slug, 100, null, new())], kind), default);
        return await catalog.StatusAsync(new(product.Id, product.Version, ProductStatus.Published), default);
    }

    private static async Task Receive(CommerceService catalog, Guid variantId, int quantity)
    {
        var balance = (await catalog.InventoryAsync(1, 100, default)).Items.Single(i => i.VariantId == variantId);
        await catalog.AdjustAsync(new(balance.Id, balance.Version, Guid.NewGuid(), quantity, "Initial shipping stock"), default);
    }

    private static async Task<OrderDto> Checkout(OrderingService ordering, OrderPaymentMethod method,
        params (Guid VariantId, int Quantity)[] lines)
    {
        var current = await ordering.GetCartAsync(default);
        var cart = await ordering.SaveCartAsync(new(current.Version,
            lines.Select(i => new CartLineInput(i.VariantId, i.Quantity)).ToList()), default);
        return await ordering.CheckoutAsync(new(cart.Version!, Guid.NewGuid(), method,
            "Shipping customer", "customer@example.test", "0901234567", "123 Test Street", cart.Total), default);
    }

    private static ConfirmPayment Receipt(OrderDto order, string reference = "SHIPPING-BANK", string source = "ManualBankTransfer") =>
        new(order.Version, Guid.NewGuid(), reference, order.Total, "VND", DateTime.UtcNow.AddSeconds(-1), source);

    private static DispatchShipment Dispatch(OrderDto order, params (Guid ItemId, int Quantity)[] lines) =>
        new(order.Version, Guid.NewGuid(), "Manual carrier", "TRACK-001",
            lines.Select(i => new ShipmentLineInput(i.ItemId, i.Quantity)).ToList());

    private static DeliverShipment Deliver(ShipmentDto shipment) => new(shipment.OrderVersion, shipment.Version, Guid.NewGuid());

    [Fact, Trait("Category", "ProviderIntegration")]
    public async Task WebflowUntrackedVariantCanCheckoutShipAndDeliverWithEmptySku()
    {
        await using var fixture = new FoundationDatabase("mssql", true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var catalog = Catalog(scope); var ordering = Ordering(scope);
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("", "Untracked", []), default);
        var key = Guid.NewGuid();
        var product = await catalog.CreateAsync(new(type.Id,type.Version,new("Unlimited","unlimited",null,[],[],[]),[],[],
            Configuration:new([], [new(key,"",100,null,[],TrackInventory:false)],key)),default);
        product = await catalog.StatusAsync(new(product.Id,product.Version,ProductStatus.Published),default);
        var order = await Checkout(ordering,OrderPaymentMethod.CashOnDelivery,(product.Variants.Single().Id,2));
        Assert.Empty(order.Items.Single().Sku);
        var shipment = await ordering.DispatchShipmentAsync(order.Id,Dispatch(order,(order.Items.Single().Id,2)),default);
        await ordering.DeliverShipmentAsync(order.Id,shipment.Id,Deliver(shipment),default);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await db.PhysicalStockReservations.ToListAsync());
        Assert.Null((await db.Set<ShipmentItem>().SingleAsync()).StockMovementId);
        Assert.Equal(0,(await catalog.InventoryAsync(1,100,default,product.Id)).Items.Single().OnHand);
    }

    private sealed class PrecisionClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(new DateTime(2026, 10, 18, 11, 0, 0, DateTimeKind.Utc).AddTicks(1234567));
    }

    private sealed class CommitFailure : DbTransactionInterceptor
    {
        public InvalidOperationException Error { get; } = new("Injected shipping failure after writes and before commit.");
        public bool WasReached { get; private set; }

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            WasReached = true;
            throw Error;
        }
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task PartialPaidShipmentsConsumeOnlyTheirHeldQuantitiesAndCompleteAfterEveryDelivery(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalog = Catalog(scope);
        var ordering = Ordering(scope);
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("PARTSHIP", "Partial shipping", []), default);
        var product = await Published(catalog, type, "partial-shirt");
        var variant = product.Variants.Single().Id;
        await Receive(catalog, variant, 5);
        var order = await Checkout(ordering, OrderPaymentMethod.Prepaid, (variant, 3));
        order = await ordering.ConfirmPaymentAsync(order.Id, Receipt(order), default);
        var item = Assert.Single(order.Items);
        var first = await ordering.DispatchShipmentAsync(order.Id, Dispatch(order, (item.Id, 2)), default);
        Assert.Equal(ShipmentStatus.Dispatched, first.Status);
        Assert.Equal(DateTimeKind.Utc, first.DispatchedAtUtc.Kind);
        Assert.Null(first.DeliveredAtUtc);
        var balance = await db.InventoryItems.AsNoTracking().SingleAsync();
        Assert.Equal(3, balance.OnHand);
        Assert.Equal(1, balance.Reserved);
        var hold = await db.PhysicalStockReservations.AsNoTracking().SingleAsync();
        Assert.Equal(3, hold.Quantity);
        Assert.Equal(2, hold.ConsumedQuantity);
        Assert.Equal(1, hold.Remaining);
        Assert.Equal(HoldState.Held, hold.State);
        var movement = await db.StockMovements.AsNoTracking().SingleAsync(m => m.Delta < 0);
        Assert.Equal(-2, movement.Delta);
        Assert.Equal(3, movement.QuantityAfter);
        Assert.Equal(movement.Id, (await db.Set<ShipmentItem>().AsNoTracking().SingleAsync()).StockMovementId);
        order = await ordering.GetOrderAsync(order.Id, false, default);
        Assert.True(order.PhysicalDispatchStarted);
        Assert.Equal(OrderFulfillmentStatus.InProgress, order.FulfillmentStatus);
        Assert.Equal(OrderStatus.AwaitingProcessing, order.Status);
        await Assert.ThrowsAsync<ConflictException>(() => ordering.DispatchShipmentAsync(order.Id, Dispatch(order, (item.Id, 2)), default));
        await Assert.ThrowsAsync<ConflictException>(() => ordering.CancelAsync(order.Id, new(order.Version), false, default));
        Assert.Equal(0, await ordering.ExpireAsync(default));
        Assert.Single(await db.Set<Shipment>().AsNoTracking().ToListAsync());
        Assert.Equal(2, await db.StockMovements.CountAsync());
        first = await ordering.DeliverShipmentAsync(order.Id, first.Id, Deliver(first), default);
        Assert.Equal(ShipmentStatus.Delivered, first.Status);
        Assert.Equal(DateTimeKind.Utc, first.DeliveredAtUtc!.Value.Kind);
        order = await ordering.GetOrderAsync(order.Id, false, default);
        Assert.Equal(OrderFulfillmentStatus.InProgress, order.FulfillmentStatus);
        var second = await ordering.DispatchShipmentAsync(order.Id, Dispatch(order, (item.Id, 1)), default);
        hold = await db.PhysicalStockReservations.AsNoTracking().SingleAsync();
        Assert.Equal(3, hold.ConsumedQuantity);
        Assert.Equal(HoldState.Consumed, hold.State);
        balance = await db.InventoryItems.AsNoTracking().SingleAsync();
        Assert.Equal(2, balance.OnHand);
        Assert.Equal(0, balance.Reserved);
        await ordering.DeliverShipmentAsync(order.Id, second.Id, Deliver(second), default);
        order = await ordering.GetOrderAsync(order.Id, false, default);
        Assert.Equal(OrderFulfillmentStatus.Completed, order.FulfillmentStatus);
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal(2, (await ordering.ShipmentsAsync(order.Id, false, default)).Count);
        Assert.Equal(-3, await db.StockMovements.Where(m => m.Delta < 0).SumAsync(m => m.Delta));
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task CodDeliveryDoesNotImplyPaymentAndCashIsAcceptedOnlyAfterAllPhysicalItemsArrive(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalog = Catalog(scope);
        var ordering = Ordering(scope);
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("CODSHIP", "COD shipping", []), default);
        var product = await Published(catalog, type, "cod-shirt");
        var variant = product.Variants.Single().Id;
        await Receive(catalog, variant, 2);
        var order = await Checkout(ordering, OrderPaymentMethod.CashOnDelivery, (variant, 2));
        var item = Assert.Single(order.Items);
        var first = await ordering.DispatchShipmentAsync(order.Id, Dispatch(order, (item.Id, 1)), default);
        order = await ordering.GetOrderAsync(order.Id, false, default);
        await Assert.ThrowsAsync<ConflictException>(() => ordering.CancelAsync(order.Id, new(order.Version), false, default));
        await Assert.ThrowsAsync<ConflictException>(() => ordering.ConfirmPaymentAsync(order.Id, Receipt(order, "COD-EARLY", "ManualCOD"), default));
        Assert.Empty(await db.PaymentReceipts.AsNoTracking().ToListAsync());
        first = await ordering.DeliverShipmentAsync(order.Id, first.Id, Deliver(first), default);
        order = await ordering.GetOrderAsync(order.Id, false, default);
        await Assert.ThrowsAsync<ConflictException>(() => ordering.ConfirmPaymentAsync(order.Id, Receipt(order, "COD-PARTIAL", "ManualCOD"), default));
        var second = await ordering.DispatchShipmentAsync(order.Id, Dispatch(order, (item.Id, 1)), default);
        await ordering.DeliverShipmentAsync(order.Id, second.Id, Deliver(second), default);
        order = await ordering.GetOrderAsync(order.Id, false, default);
        Assert.Equal(OrderFulfillmentStatus.Completed, order.FulfillmentStatus);
        Assert.Equal(OrderPaymentStatus.Unpaid, order.PaymentStatus);
        Assert.Equal(OrderStatus.AwaitingProcessing, order.Status);
        Assert.Null(order.PaidAtUtc);
        await Assert.ThrowsAsync<DomainException>(() => ordering.ConfirmPaymentAsync(order.Id,
            Receipt(order, "COD-SHORT", "ManualCOD") with { Amount = order.Total - 1 }, default));
        var cashRequest = Receipt(order, "COD-COLLECTED", "ManualCOD");
        var paid = await ordering.ConfirmPaymentAsync(order.Id, cashRequest, default);
        Assert.Equal(OrderPaymentStatus.Paid, paid.PaymentStatus);
        Assert.Equal(OrderFulfillmentStatus.Completed, paid.FulfillmentStatus);
        Assert.Equal(OrderStatus.Completed, paid.Status);
        Assert.Equal("ManualCOD", (await db.PaymentReceipts.AsNoTracking().SingleAsync()).Source);
        Assert.Equal(paid.Version, (await ordering.ConfirmPaymentAsync(order.Id, cashRequest, default)).Version);
        await Assert.ThrowsAsync<ConflictException>(() => ordering.ConfirmPaymentAsync(order.Id,
            cashRequest with { Source = "ManualBankTransfer" }, default));
        var balance = await db.InventoryItems.AsNoTracking().SingleAsync();
        Assert.Equal(0, balance.OnHand);
        Assert.Equal(0, balance.Reserved);
        Assert.Equal(3, await db.StockMovements.CountAsync());
        Assert.Single(await db.PaymentReceipts.AsNoTracking().ToListAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task BankConfirmationAfterPartialCodDispatchDoesNotReserveOrReissueAlreadyShippedStock(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalog = Catalog(scope);
        var ordering = Ordering(scope);
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("CODBANK", "COD paid by bank", []), default);
        var product = await Published(catalog, type, "cod-bank-shirt");
        var variant = product.Variants.Single().Id;
        await Receive(catalog, variant, 2);
        var order = await Checkout(ordering, OrderPaymentMethod.CashOnDelivery, (variant, 2));
        var item = Assert.Single(order.Items);
        var first = await ordering.DispatchShipmentAsync(order.Id, Dispatch(order, (item.Id, 1)), default);
        order = await ordering.GetOrderAsync(order.Id, false, default);
        order = await ordering.ConfirmPaymentAsync(order.Id, Receipt(order, "COD-BANK-MIDWAY"), default);
        Assert.Equal(OrderPaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal(OrderFulfillmentStatus.InProgress, order.FulfillmentStatus);
        var balance = await db.InventoryItems.AsNoTracking().SingleAsync();
        Assert.Equal(1, balance.OnHand);
        Assert.Equal(1, balance.Reserved);
        var hold = await db.PhysicalStockReservations.AsNoTracking().SingleAsync();
        Assert.Equal(1, hold.ConsumedQuantity);
        Assert.Equal(HoldState.Held, hold.State);
        Assert.Equal(2, await db.StockMovements.CountAsync());
        first = await ordering.DeliverShipmentAsync(order.Id, first.Id,
            Deliver(first) with { OrderVersion = order.Version }, default);
        order = await ordering.GetOrderAsync(order.Id, false, default);
        var second = await ordering.DispatchShipmentAsync(order.Id, Dispatch(order, (item.Id, 1)), default);
        await ordering.DeliverShipmentAsync(order.Id, second.Id, Deliver(second), default);
        order = await ordering.GetOrderAsync(order.Id, false, default);
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal(OrderFulfillmentStatus.Completed, order.FulfillmentStatus);
        balance = await db.InventoryItems.AsNoTracking().SingleAsync();
        Assert.Equal(0, balance.OnHand);
        Assert.Equal(0, balance.Reserved);
        Assert.Empty(await db.DigitalDeliveries.AsNoTracking().ToListAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ShippingAMixedOrderKeepsDigitalDeliveryStableAndManualExtensionPending(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalog = Catalog(scope);
        var ordering = Ordering(scope);
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("MIXSHIP", "Mixed shipping", []), default);
        var physical = await Published(catalog, type, "mixed-shipping-shirt");
        var unique = await Published(catalog, type, "mixed-shipping-code", ProductFulfillmentKind.DigitalUnique);
        var extension = await Published(catalog, type, "mixed-shipping-renewal", ProductFulfillmentKind.ManualExtension);
        await Receive(catalog, physical.Variants.Single().Id, 1);
        await catalog.ImportDigitalAsync(new(unique.Id, unique.Variants.Single().Id, unique.Version,
            [new("ONLY", "MixedShippingSecret")]), default);
        var order = await Checkout(ordering, OrderPaymentMethod.Prepaid, (physical.Variants.Single().Id, 1),
            (unique.Variants.Single().Id, 1), (extension.Variants.Single().Id, 1));
        order = await ordering.ConfirmPaymentAsync(order.Id, Receipt(order), default);
        var before = Assert.Single(await ordering.DigitalDeliveriesAsync(order.Id, default));
        var outboxCount = await db.OutboxMessages.CountAsync();
        var item = order.Items.Single(i => i.FulfillmentKind == ProductFulfillmentKind.Physical);
        var shipment = await ordering.DispatchShipmentAsync(order.Id, Dispatch(order, (item.Id, 1)), default);
        await ordering.DeliverShipmentAsync(order.Id, shipment.Id, Deliver(shipment), default);
        order = await ordering.GetOrderAsync(order.Id, false, default);
        Assert.Equal(OrderPaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal(OrderStatus.AwaitingProcessing, order.Status);
        Assert.Equal(OrderFulfillmentStatus.InProgress, order.FulfillmentStatus);
        var after = Assert.Single(await ordering.DigitalDeliveriesAsync(order.Id, default));
        Assert.Equal(before.Id, after.Id);
        Assert.Equal("MixedShippingSecret", after.Content);
        Assert.Equal(DigitalStockState.Delivered, (await db.DigitalStockItems.AsNoTracking().SingleAsync()).State);
        Assert.Equal(outboxCount, await db.OutboxMessages.CountAsync());
        Assert.DoesNotContain("MixedShippingSecret", JsonSerializer.Serialize(await ordering.ShipmentsAsync(order.Id, false, default)));
        Assert.Single(await db.DigitalDeliveries.AsNoTracking().ToListAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ShipmentOperationsReplayWithoutVersionsAndRejectChangedPayloadCrossOrderAndDeliveryReuse(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalog = Catalog(scope);
        var ordering = Ordering(scope, clock: new PrecisionClock());
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("SHIPREPLAY", "Shipping replay", []), default);
        var product = await Published(catalog, type, "shipping-replay-shirt");
        var variant = product.Variants.Single().Id;
        await Receive(catalog, variant, 3);
        var firstOrder = await Checkout(ordering, OrderPaymentMethod.CashOnDelivery, (variant, 2));
        var secondOrder = await Checkout(ordering, OrderPaymentMethod.CashOnDelivery, (variant, 1));
        var firstItem = Assert.Single(firstOrder.Items);
        var request = Dispatch(firstOrder, (firstItem.Id, 1));
        var first = await ordering.DispatchShipmentAsync(firstOrder.Id, request, default);
        Assert.Equal(first.Id, (await ordering.DispatchShipmentAsync(firstOrder.Id, request, default)).Id);
        await Assert.ThrowsAsync<ConflictException>(() => ordering.DispatchShipmentAsync(firstOrder.Id,
            request with { Carrier = "Different carrier" }, default));
        await Assert.ThrowsAsync<ConflictException>(() => ordering.DispatchShipmentAsync(firstOrder.Id,
            request with { Items = [new(firstItem.Id, 2)] }, default));
        await Assert.ThrowsAsync<ConflictException>(() => ordering.DispatchShipmentAsync(secondOrder.Id,
            request with { OrderVersion = secondOrder.Version, Items = [new(secondOrder.Items.Single().Id, 1)] }, default));
        await Assert.ThrowsAsync<ConflictException>(() => ordering.DispatchShipmentAsync(firstOrder.Id,
            request with { OperationId = Guid.NewGuid() }, default));
        var deliverRequest = Deliver(first);
        first = await ordering.DeliverShipmentAsync(firstOrder.Id, first.Id, deliverRequest, default);
        var replay = await ordering.DeliverShipmentAsync(firstOrder.Id, first.Id, deliverRequest, default);
        Assert.Equal(first.Version, replay.Version);
        Assert.Equal(first.DeliveredAtUtc, replay.DeliveredAtUtc);
        await Assert.ThrowsAsync<ConflictException>(() => ordering.DeliverShipmentAsync(firstOrder.Id, first.Id,
            Deliver(first), default));
        var second = await ordering.DispatchShipmentAsync(secondOrder.Id, Dispatch(secondOrder, (secondOrder.Items.Single().Id, 1)), default);
        await Assert.ThrowsAsync<ConflictException>(() => ordering.DeliverShipmentAsync(secondOrder.Id, second.Id,
            Deliver(second) with { OperationId = deliverRequest.OperationId }, default));
        Assert.Equal(ShipmentStatus.Delivered, (await ordering.DispatchShipmentAsync(firstOrder.Id, request, default)).Status);
        Assert.Equal(2, await db.Set<Shipment>().CountAsync());
        Assert.Equal(3, await db.StockMovements.CountAsync());
        Assert.Equal(1, (await db.InventoryItems.AsNoTracking().SingleAsync()).Reserved);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ConcurrentDispatchesOfTheFinalHeldQuantityCreateOnlyOneShipmentAndLedgerMovement(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var owner = Guid.NewGuid();
        OrderDto order;
        using (var scope = fixture.Scope(owner))
        {
            var catalog = Catalog(scope);
            await catalog.InitializeAsync(default);
            var type = await catalog.CreateTypeAsync(new("SHIPRACE", "Shipping race", []), default);
            var product = await Published(catalog, type, "shipping-last-shirt");
            var variant = product.Variants.Single().Id;
            await Receive(catalog, variant, 1);
            order = await Checkout(Ordering(scope), OrderPaymentMethod.CashOnDelivery, (variant, 1));
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<ShipmentDto?> Attempt()
        {
            using var scope = fixture.Scope(Guid.NewGuid());
            await start.Task;
            try { return await Ordering(scope).DispatchShipmentAsync(order.Id, Dispatch(order, (order.Items.Single().Id, 1)), timeout.Token); }
            catch (ConflictException) { return null; }
        }
        var first = Attempt();
        var second = Attempt();
        start.SetResult();
        Assert.Single((await Task.WhenAll(first, second)).Where(i => i is not null));
        using var verification = fixture.Scope(owner);
        var db = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var balance = await db.InventoryItems.AsNoTracking().SingleAsync();
        Assert.Equal(0, balance.OnHand);
        Assert.Equal(0, balance.Reserved);
        Assert.Equal(1, (await db.PhysicalStockReservations.AsNoTracking().SingleAsync()).ConsumedQuantity);
        Assert.Single(await db.Set<Shipment>().AsNoTracking().ToListAsync());
        Assert.Single(await db.Set<ShipmentItem>().AsNoTracking().ToListAsync());
        Assert.Single(await db.StockMovements.Where(m => m.Delta == -1).AsNoTracking().ToListAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ShippingChecksDedicatedServicePermissionsOwnershipAndPrepaidStateBeforeConsumingStock(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var owner = Guid.NewGuid();
        using var scope = fixture.Scope(owner);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalog = Catalog(scope);
        var ordering = Ordering(scope);
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("SHIPACL", "Shipping ACL", []), default);
        var product = await Published(catalog, type, "shipping-permissions-shirt");
        var variant = product.Variants.Single().Id;
        await Receive(catalog, variant, 1);
        var order = await Checkout(ordering, OrderPaymentMethod.Prepaid, (variant, 1));
        var request = Dispatch(order, (order.Items.Single().Id, 1));
        await Assert.ThrowsAsync<ForbiddenException>(() => Ordering(scope, "Permissions.Shipping.Dispatch").DispatchShipmentAsync(order.Id, request, default));
        await Assert.ThrowsAsync<ConflictException>(() => ordering.DispatchShipmentAsync(order.Id, request, default));
        Assert.Empty(await db.Set<Shipment>().AsNoTracking().ToListAsync());
        Assert.Equal(1, (await db.InventoryItems.AsNoTracking().SingleAsync()).OnHand);
        using var otherScope = fixture.Scope(Guid.NewGuid());
        await Assert.ThrowsAsync<NotFoundException>(() => Ordering(otherScope).ShipmentsAsync(order.Id, false, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => Ordering(otherScope, "Permissions.Shipping.View").ShipmentsAsync(order.Id, true, default));
        Assert.Empty(await Ordering(otherScope).ShipmentsAsync(order.Id, true, default));
        order = await ordering.ConfirmPaymentAsync(order.Id, Receipt(order), default);
        var shipment = await ordering.DispatchShipmentAsync(order.Id, Dispatch(order, (order.Items.Single().Id, 1)), default);
        await Assert.ThrowsAsync<ForbiddenException>(() => Ordering(scope, "Permissions.Shipping.Deliver").DeliverShipmentAsync(order.Id, shipment.Id, Deliver(shipment), default));
        Assert.Equal(ShipmentStatus.Dispatched, (await db.Set<Shipment>().AsNoTracking().SingleAsync()).Status);
        Assert.Single(await Ordering(scope, "Permissions.Shipping.View").ShipmentsAsync(order.Id, false, default));
        Assert.Empty(await db.Set<Shipment>().Where(s => s.Status == ShipmentStatus.Delivered).ToListAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task MultilineDispatchRejectsForeignDigitalDuplicateAndExcessLinesWithoutPartialConsumption(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalog = Catalog(scope);
        var ordering = Ordering(scope);
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("SHIPLINES", "Shipping lines", []), default);
        var shirt = await Published(catalog, type, "multiline-shipping-shirt");
        var trousers = await Published(catalog, type, "multiline-shipping-trousers");
        var code = await Published(catalog, type, "multiline-shipping-code", ProductFulfillmentKind.DigitalUnique);
        await Receive(catalog, shirt.Variants.Single().Id, 2);
        await Receive(catalog, trousers.Variants.Single().Id, 2);
        await catalog.ImportDigitalAsync(new(code.Id, code.Variants.Single().Id, code.Version,
            [new("ONLY", "MultilineShippingSecret")]), default);
        var order = await Checkout(ordering, OrderPaymentMethod.Prepaid, (shirt.Variants.Single().Id, 1),
            (trousers.Variants.Single().Id, 1), (code.Variants.Single().Id, 1));
        order = await ordering.ConfirmPaymentAsync(order.Id, Receipt(order), default);
        var other = await Checkout(ordering, OrderPaymentMethod.CashOnDelivery, (shirt.Variants.Single().Id, 1));
        var shirtItem = order.Items.Single(i => i.ProductId == shirt.Id);
        var trousersItem = order.Items.Single(i => i.ProductId == trousers.Id);
        var digitalItem = order.Items.Single(i => i.ProductId == code.Id);
        await Assert.ThrowsAsync<ConflictException>(() => ordering.DispatchShipmentAsync(order.Id,
            Dispatch(order, (shirtItem.Id, 1), (other.Items.Single().Id, 1)), default));
        await Assert.ThrowsAsync<ConflictException>(() => ordering.DispatchShipmentAsync(order.Id,
            Dispatch(order, (shirtItem.Id, 1), (digitalItem.Id, 1)), default));
        await Assert.ThrowsAsync<ConflictException>(() => ordering.DispatchShipmentAsync(order.Id,
            Dispatch(order, (shirtItem.Id, 1), (trousersItem.Id, 2)), default));
        await Assert.ThrowsAsync<DomainException>(() => ordering.DispatchShipmentAsync(order.Id,
            Dispatch(order, (shirtItem.Id, 1), (shirtItem.Id, 1)), default));
        await Assert.ThrowsAsync<DomainException>(() => ordering.DispatchShipmentAsync(order.Id, Dispatch(order), default));
        await Assert.ThrowsAsync<ConflictException>(() => ordering.ConfirmPaymentAsync(other.Id,
            Receipt(other, "UNDELIVERED-COD", "ManualCOD"), default));
        Assert.Empty(await db.Set<Shipment>().AsNoTracking().ToListAsync());
        Assert.Equal(2, await db.StockMovements.CountAsync());
        Assert.Equal(4, await db.InventoryItems.SumAsync(i => i.OnHand));
        Assert.Equal(3, await db.InventoryItems.SumAsync(i => i.Reserved));
        Assert.All(await db.PhysicalStockReservations.AsNoTracking().ToListAsync(), r => Assert.Equal(0, r.ConsumedQuantity));
        Assert.Equal(order.Version, (await ordering.GetOrderAsync(order.Id, false, default)).Version);
        var request = Dispatch(order, (shirtItem.Id, 1), (trousersItem.Id, 1));
        var shipment = await ordering.DispatchShipmentAsync(order.Id, request, default);
        Assert.Equal(2, shipment.Items.Count);
        Assert.Equal(shipment.Id, (await ordering.DispatchShipmentAsync(order.Id,
            request with { Items = request.Items.AsEnumerable().Reverse().ToList() }, default)).Id);
        Assert.Equal(2, await db.InventoryItems.SumAsync(i => i.OnHand));
        Assert.Equal(1, await db.InventoryItems.SumAsync(i => i.Reserved));
        Assert.Equal(4, await db.StockMovements.CountAsync());
        Assert.Single(await db.DigitalDeliveries.AsNoTracking().ToListAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task CommitFailureRollsBackShipmentStockReservationLedgerOrderAndAuditBeforeSafeRetry(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalog = Catalog(scope);
        var ordering = Ordering(scope);
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("SHIPROLLBACK", "Shipping rollback", []), default);
        var product = await Published(catalog, type, "shipping-rollback-shirt");
        var variant = product.Variants.Single().Id;
        await Receive(catalog, variant, 1);
        var order = await Checkout(ordering, OrderPaymentMethod.CashOnDelivery, (variant, 1));
        var request = Dispatch(order, (order.Items.Single().Id, 1));
        var stockBefore = await db.InventoryItems.AsNoTracking().SingleAsync();
        var holdBefore = await db.PhysicalStockReservations.AsNoTracking().SingleAsync();
        var auditCount = await db.Trails.CountAsync();
        var outboxCount = await db.OutboxMessages.CountAsync();
        var failure = new CommitFailure();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>(scope.ServiceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>())
            .AddInterceptors(failure).Options;
        await using (var failingDb = new ApplicationDbContext(options,
            scope.ServiceProvider.GetRequiredService<ICurrentUser>(), scope.ServiceProvider.GetRequiredService<ISerializerService>(),
            scope.ServiceProvider.GetRequiredService<IEventPublisher>()))
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Ordering(scope, dbOverride: failingDb).DispatchShipmentAsync(order.Id, request, default));
            Assert.Same(failure.Error, error);
            Assert.True(failure.WasReached);
            Assert.Empty(failingDb.ChangeTracker.Entries());
        }
        Assert.Empty(await db.Set<Shipment>().AsNoTracking().ToListAsync());
        Assert.Empty(await db.Set<ShipmentItem>().AsNoTracking().ToListAsync());
        Assert.Single(await db.StockMovements.AsNoTracking().ToListAsync());
        Assert.Equal(auditCount, await db.Trails.CountAsync());
        Assert.Equal(outboxCount, await db.OutboxMessages.CountAsync());
        var stockAfter = await db.InventoryItems.AsNoTracking().SingleAsync();
        Assert.Equal(stockBefore.Version, stockAfter.Version);
        Assert.Equal(1, stockAfter.OnHand);
        Assert.Equal(1, stockAfter.Reserved);
        var holdAfter = await db.PhysicalStockReservations.AsNoTracking().SingleAsync();
        Assert.Equal(holdBefore.Version, holdAfter.Version);
        Assert.Equal(0, holdAfter.ConsumedQuantity);
        Assert.Equal(HoldState.Held, holdAfter.State);
        Assert.Equal(order.Version, (await ordering.GetOrderAsync(order.Id, false, default)).Version);
        var dispatched = await ordering.DispatchShipmentAsync(order.Id, request, default);
        Assert.Equal(dispatched.Id, (await ordering.DispatchShipmentAsync(order.Id, request, default)).Id);
        Assert.Single(await db.Set<Shipment>().AsNoTracking().ToListAsync());
        Assert.Single(await db.Set<ShipmentItem>().AsNoTracking().ToListAsync());
        Assert.Equal(2, await db.StockMovements.CountAsync());
        stockAfter = await db.InventoryItems.AsNoTracking().SingleAsync();
        Assert.Equal(0, stockAfter.OnHand);
        Assert.Equal(0, stockAfter.Reserved);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task DatabaseAndPersistenceProtectShipmentOwnershipMovementUniquenessAndImmutableHistory(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var owner = Guid.NewGuid();
        using var scope = fixture.Scope(owner);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalog = Catalog(scope);
        var ordering = Ordering(scope);
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new("SHIPDB", "Shipping database", []), default);
        var product = await Published(catalog, type, "shipping-database-shirt");
        var variant = product.Variants.Single().Id;
        await Receive(catalog, variant, 2);
        var first = await Checkout(ordering, OrderPaymentMethod.CashOnDelivery, (variant, 1));
        var firstShipment = await ordering.DispatchShipmentAsync(first.Id, Dispatch(first, (first.Items.Single().Id, 1)), default);
        var second = await Checkout(ordering, OrderPaymentMethod.CashOnDelivery, (variant, 1));
        var secondShipment = await ordering.DispatchShipmentAsync(second.Id, Dispatch(second, (second.Items.Single().Id, 1)), default);
        var firstItem = await db.Set<ShipmentItem>().AsNoTracking().SingleAsync(i => i.ShipmentId == firstShipment.Id);
        var secondItem = await db.Set<ShipmentItem>().AsNoTracking().SingleAsync(i => i.ShipmentId == secondShipment.Id);
        async Task Reject(FormattableString sql)
        {
            var error = await Record.ExceptionAsync(() => db.Database.ExecuteSqlInterpolatedAsync(sql));
            Assert.True(error is Microsoft.Data.SqlClient.SqlException { Number: 547 or 2601 or 2627 } ||
                error is Npgsql.PostgresException { SqlState: "23503" or "23505" or "23514" }, error?.ToString() ?? "Constraint was not enforced.");
        }
        await Reject($"UPDATE \"Shipping\".\"ShipmentItems\" SET \"OrderItemId\"={secondItem.OrderItemId} WHERE \"ShipmentId\"={firstShipment.Id}");
        await Reject($"UPDATE \"Shipping\".\"ShipmentItems\" SET \"StockMovementId\"={firstItem.StockMovementId} WHERE \"ShipmentId\"={secondShipment.Id}");
        await Reject($"UPDATE \"Shipping\".\"ShipmentItems\" SET \"FulfillmentKind\"={2} WHERE \"ShipmentId\"={firstShipment.Id}");
        await Reject($"UPDATE \"Shipping\".\"ShipmentItems\" SET \"Quantity\"={0} WHERE \"ShipmentId\"={firstShipment.Id}");
        await Reject($"UPDATE \"Ordering\".\"PhysicalStockReservations\" SET \"ConsumedQuantity\"={2} WHERE \"OrderItemId\"={firstItem.OrderItemId}");
        await Reject($"DELETE FROM \"Ordering\".\"Orders\" WHERE \"Id\"={first.Id}");
        using (var historyScope = fixture.Scope(owner))
        {
            var historyDb = historyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            historyDb.Remove(await historyDb.Set<ShipmentItem>().FirstAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => historyDb.SaveChangesAsync());
        }
        using (var historyScope = fixture.Scope(owner))
        {
            var historyDb = historyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = await historyDb.Set<ShipmentItem>().FirstAsync();
            historyDb.Entry(row).Property(i => i.Quantity).CurrentValue = 2;
            await Assert.ThrowsAsync<InvalidOperationException>(() => historyDb.SaveChangesAsync());
        }
        Assert.Equal(2, await db.Set<Shipment>().CountAsync());
        Assert.Equal(2, await db.Set<ShipmentItem>().CountAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task HttpShippingEnforcesAuthenticationDedicatedAclOwnerVisibilityAndUtcNoStoreResponses(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var oldEmail = Environment.GetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL");
        var oldPassword = Environment.GetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD");
        try
        {
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL", "shipping@example.test");
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD", "ShippingPassword!29874");
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
            owner = Guid.Parse(await db.Users.Where(u => u.Email == "shipping@example.test").Select(u => u.Id).SingleAsync());
            var catalog = Catalog(scope);
            await catalog.InitializeAsync(default);
            var type = await catalog.CreateTypeAsync(new("HTTPSHIP", "HTTP shipping", []), default);
            var product = await Published(catalog, type, "http-shipping-shirt");
            variant = product.Variants.Single().Id;
            await Receive(catalog, variant, 2);
        }
        OrderDto mine;
        using (var scope = fixture.Scope(owner))
            mine = await Checkout(Ordering(scope), OrderPaymentMethod.CashOnDelivery, (variant, 1));
        OrderDto other;
        using (var scope = fixture.Scope(Guid.NewGuid()))
            other = await Checkout(Ordering(scope), OrderPaymentMethod.CashOnDelivery, (variant, 1));
        var request = Dispatch(mine, (mine.Items.Single().Id, 1));
        await using var app = await TestHostApplication.StartAsync(fixture, true);
        using var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/orders/{mine.Id}/shipments")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync($"/api/admin/orders/{mine.Id}/shipments", request)).StatusCode);
        var login = await client.PostAsJsonAsync("/api/tokens/get", new TokenRequest("shipping@example.test", "ShippingPassword!29874"));
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", token.Token);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/orders/{other.Id}/shipments")).StatusCode);
        var response = await client.GetAsync($"/api/orders/{mine.Id}/shipments");
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Empty((await response.Content.ReadFromJsonAsync<List<ShipmentDto>>())!);
        response = await client.PostAsJsonAsync($"/api/admin/orders/{mine.Id}/shipments", request);
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        var shipment = (await response.Content.ReadFromJsonAsync<ShipmentDto>())!;
        Assert.Equal(DateTimeKind.Utc, shipment.DispatchedAtUtc.Kind);
        response = await client.PostAsJsonAsync($"/api/admin/orders/{mine.Id}/shipments/{shipment.Id}/deliver", Deliver(shipment));
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        shipment = (await response.Content.ReadFromJsonAsync<ShipmentDto>())!;
        Assert.Equal(DateTimeKind.Utc, shipment.DeliveredAtUtc!.Value.Kind);
        response = await client.GetAsync($"/api/admin/orders/{other.Id}/shipments");
        response.EnsureSuccessStatusCode();
        using (var scope = fixture.Scope(owner))
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var functionId = await db.Functions.Where(f => f.Code == "Shipping").Select(f => f.Id).SingleAsync();
            var actionIds = await db.Actions.Where(a => a.Code == "View" || a.Code == "Dispatch" || a.Code == "Deliver").Select(a => a.Id).ToListAsync();
            await db.Permissions.Where(p => p.FunctionId == functionId && actionIds.Contains(p.ActionId))
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.DeletedOn, DateTime.UtcNow));
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/admin/orders/{mine.Id}/shipments", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/admin/orders/{mine.Id}/shipments/{shipment.Id}/deliver", Deliver(shipment))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/admin/orders/{mine.Id}/shipments")).StatusCode);
        response = await client.GetAsync($"/api/orders/{mine.Id}/shipments");
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(ShipmentStatus.Delivered, Assert.Single((await response.Content.ReadFromJsonAsync<List<ShipmentDto>>())!).Status);
    }
}
