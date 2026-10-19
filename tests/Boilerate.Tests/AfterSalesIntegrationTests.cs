using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
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
using Boilerate.Domain.Payments;
using Boilerate.Domain.Renewals;
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
public sealed class AfterSalesIntegrationTests
{
    private static CommerceService Catalog(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = Substitute.For<IUserService>();
        users.HasPermissionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        return new(db, new CommerceUnitOfWork(db), scope.ServiceProvider.GetRequiredService<ICurrentUser>(), users,
            Options.Create(new CommerceSettings()), scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>());
    }

    private static OrderingService Ordering(IServiceScope scope, string? deniedPermission = null, ApplicationDbContext? dbOverride = null)
    {
        var db = dbOverride ?? scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = Substitute.For<IUserService>();
        users.HasPermissionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => (string)call[1] != deniedPermission);
        return new(db, new CommerceUnitOfWork(db), scope.ServiceProvider.GetRequiredService<ICurrentUser>(), users,
            Options.Create(new OrderingSettings()), TimeProvider.System,
            scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>());
    }

    private static async Task<ProductDto> Product(IServiceScope scope, string slug,
        ProductFulfillmentKind kind = ProductFulfillmentKind.Physical, int stock = 10)
    {
        var catalog = Catalog(scope);
        await catalog.InitializeAsync(default);
        var type = await catalog.CreateTypeAsync(new(slug.ToUpperInvariant(), "After-sales products", []), default);
        var product = await catalog.CreateAsync(new(type.Id, type.Version,
            new("Product " + slug, slug, null, [], [], []), [], [new(slug, 100, null, new())], kind), default);
        product = await catalog.StatusAsync(new(product.Id, product.Version, ProductStatus.Published), default);
        if (kind == ProductFulfillmentKind.Physical)
        {
            var balance = (await catalog.InventoryAsync(1, 100, default)).Items.Single(i => i.VariantId == product.Variants.Single().Id);
            await catalog.AdjustAsync(new(balance.Id, balance.Version, Guid.NewGuid(), stock, "Initial after-sales stock"), default);
        }
        return product;
    }

    private static async Task<OrderDto> Checkout(OrderingService ordering, OrderPaymentMethod method,
        params (Guid VariantId, int Quantity)[] lines)
    {
        var current = await ordering.GetCartAsync(default);
        var cart = await ordering.SaveCartAsync(new(current.Version,
            lines.Select(i => new CartLineInput(i.VariantId, i.Quantity)).ToList()), default);
        return await ordering.CheckoutAsync(new(cart.Version!, Guid.NewGuid(), method,
            "After-sales customer", "customer@example.test", "0901234567", "123 Test Street", cart.Total), default);
    }

    private static ConfirmPayment Receipt(OrderDto order) => new(order.Version, Guid.NewGuid(),
        "BANK-" + Guid.NewGuid().ToString("N"), order.Total, "VND", DateTime.UtcNow.AddSeconds(-2));

    private static ConfirmRefund Refund(OrderDto order, decimal? amount = null) => new(order.Version, Guid.NewGuid(),
        "REFUND-" + Guid.NewGuid().ToString("N"), amount ?? order.Total, "VND", DateTime.UtcNow, "Payout checked against the bank statement.");

    private static DispatchShipment Dispatch(OrderDto order, params (Guid ItemId, int Quantity)[] lines) =>
        new(order.Version, Guid.NewGuid(), "Manual carrier", "TRACK-" + Guid.NewGuid().ToString("N"),
            lines.Select(i => new ShipmentLineInput(i.ItemId, i.Quantity)).ToList());

    private static ReceiveReturn Return(OrderDto order, ShipmentDto shipment, int quantity, int restock, bool closeCod = false) =>
        new(order.Version, Guid.NewGuid(), "RETURN-" + Guid.NewGuid().ToString("N"), "Received and inspected at the shop.",
            [new(shipment.Id, shipment.Items.Single().OrderItemId, quantity, restock)], closeCod);

    private static async Task Reject(Func<Task> action)
    {
        var error = await Record.ExceptionAsync(action);
        Assert.True(error is DomainException or ConflictException, error?.ToString() ?? "The invalid after-sales command was accepted.");
    }

    private sealed class CommitFailure : DbTransactionInterceptor
    {
        public InvalidOperationException Error { get; } = new("Injected after-sales failure after writes and before commit.");
        public bool WasReached { get; private set; }
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            WasReached = true;
            throw Error;
        }
    }

    private static ApplicationDbContext FailingContext(IServiceScope scope, CommitFailure failure) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>(scope.ServiceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>())
            .AddInterceptors(failure).Options,
            scope.ServiceProvider.GetRequiredService<ICurrentUser>(), scope.ServiceProvider.GetRequiredService<ISerializerService>(),
            scope.ServiceProvider.GetRequiredService<IEventPublisher>());

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task RefundLedgerCapsPaidAmountAndPartialRefundStillAllowsDelivery(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await Product(scope, "partial-refund");
        var ordering = Ordering(scope);
        var order = await Checkout(ordering, OrderPaymentMethod.Prepaid, (product.Variants.Single().Id, 2));
        await Reject(() => ordering.ConfirmRefundAsync(order.Id, Refund(order, 50), default));
        var bankTime = DateTime.UtcNow.AddSeconds(-2);
        bankTime = new DateTime(bankTime.Ticks - bankTime.Ticks % 10 + 7, DateTimeKind.Utc);
        order = await ordering.ConfirmPaymentAsync(order.Id, Receipt(order) with { ReceivedAtUtc = bankTime }, default);
        var storedReceiptTime = DateTime.SpecifyKind((await db.PaymentReceipts.AsNoTracking().SingleAsync()).ReceivedAtUtc, DateTimeKind.Utc);
        Assert.Equal(bankTime.Ticks / 10, storedReceiptTime.Ticks / 10);
        // Refund and receipt at the same instant must agree across datetime2 and PostgreSQL's microseconds.
        var refundCommand = Refund(order, 50) with { RefundedAtUtc = bankTime };
        var refund = await ordering.ConfirmRefundAsync(order.Id, refundCommand, default);
        Assert.Equal(bankTime.Ticks - bankTime.Ticks % 10, refund.Refund.RefundedAtUtc.Ticks);
        await Assert.ThrowsAsync<DomainException>(() => ordering.ConfirmRefundAsync(order.Id,
            refundCommand with { Amount = refundCommand.Amount + 0.001m }, default));
        Assert.False(refund.Closed);
        order = await ordering.GetOrderAsync(order.Id, false, default);
        Assert.Equal(OrderPaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal(OrderFulfillmentStatus.InProgress, order.FulfillmentStatus);
        var summary = await ordering.AfterSalesAsync(order.Id, false, default);
        Assert.Equal(200, summary.ReceivedAmount);
        Assert.Equal(50, summary.RefundedAmount);
        Assert.Equal(150, summary.RefundableAmount);
        await Reject(() => ordering.ConfirmRefundAsync(order.Id, Refund(order, 151), default));
        await Reject(() => ordering.ConfirmRefundAsync(order.Id, Refund(order, 0), default));
        await Reject(() => ordering.ConfirmRefundAsync(order.Id, Refund(order, 1.5m), default));
        await Reject(() => ordering.ConfirmRefundAsync(order.Id, Refund(order, 1) with { Currency = "USD" }, default));
        await Reject(() => ordering.ConfirmRefundAsync(order.Id, Refund(order, 1) with { RefundedAtUtc = DateTime.UtcNow.AddDays(1) }, default));
        await Reject(() => ordering.ConfirmRefundAsync(order.Id, Refund(order, 1) with { RefundedAtUtc = DateTime.UtcNow.AddDays(-1) }, default));
        var shipment = await ordering.DispatchShipmentAsync(order.Id, Dispatch(order, (order.Items.Single().Id, 2)), default);
        await ordering.DeliverShipmentAsync(order.Id, shipment.Id, new(shipment.OrderVersion, shipment.Version, Guid.NewGuid()), default);
        Assert.Equal(OrderStatus.Completed, (await ordering.GetOrderAsync(order.Id, false, default)).Status);
        Assert.Single(await db.Set<Refund>().AsNoTracking().ToListAsync());
        Assert.Empty(await db.Set<PhysicalReturn>().AsNoTracking().ToListAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task RefundReplayRejectsDifferentActorPayloadAndReferenceWhileIgnoringStaleVersion(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var owner = Guid.NewGuid();
        using var scope = fixture.Scope(owner);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await Product(scope, "refund-replay");
        var ordering = Ordering(scope);
        var order = await Checkout(ordering, OrderPaymentMethod.Prepaid, (product.Variants.Single().Id, 1));
        order = await ordering.ConfirmPaymentAsync(order.Id, Receipt(order), default);
        var command = Refund(order, 25);
        var first = await ordering.ConfirmRefundAsync(order.Id, command, default);
        var replay = await ordering.ConfirmRefundAsync(order.Id, command with { OrderVersion = "lost-response-stale" }, default);
        Assert.Equal(first.Refund, replay.Refund);
        Assert.Equal(first.OrderVersion, replay.OrderVersion);
        using var other = fixture.Scope(Guid.NewGuid());
        await Assert.ThrowsAsync<ConflictException>(() => Ordering(other).ConfirmRefundAsync(order.Id, command, default));
        await Assert.ThrowsAsync<ConflictException>(() => ordering.ConfirmRefundAsync(order.Id, command with { Amount = 26 }, default));
        order = await ordering.GetOrderAsync(order.Id, false, default);
        await Assert.ThrowsAsync<ConflictException>(() => ordering.ConfirmRefundAsync(order.Id,
            command with { OperationId = Guid.NewGuid(), OrderVersion = order.Version, Reference = command.Reference.ToLowerInvariant() }, default));
        await Assert.ThrowsAsync<ConflictException>(() => ordering.ConfirmRefundAsync(order.Id,
            Refund(order, 25) with { OrderVersion = "stale" }, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => Ordering(scope, "Permissions.Refund.Confirm")
            .ConfirmRefundAsync(order.Id, command, default));
        Assert.Single(await db.Set<Refund>().AsNoTracking().ToListAsync());
        Assert.Equal(75, (await ordering.AfterSalesAsync(order.Id, false, default)).RefundableAmount);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ConcurrentRefundsUseSeparateContextsAndCannotExceedTheReceipt(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var owner = Guid.NewGuid();
        OrderDto order;
        using (var setup = fixture.Scope(owner))
        {
            var product = await Product(setup, "refund-race");
            var ordering = Ordering(setup);
            order = await Checkout(ordering, OrderPaymentMethod.Prepaid, (product.Variants.Single().Id, 1));
            order = await ordering.ConfirmPaymentAsync(order.Id, Receipt(order), default);
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<RefundResult?> Attempt()
        {
            using var scope = fixture.Scope(Guid.NewGuid());
            await start.Task;
            try { return await Ordering(scope).ConfirmRefundAsync(order.Id, Refund(order, 75), timeout.Token); }
            catch (ConflictException) { return null; }
        }
        var first = Attempt();
        var second = Attempt();
        start.SetResult();
        Assert.Single((await Task.WhenAll(first, second)).Where(i => i is not null));
        using var verification = fixture.Scope(owner);
        var summary = await Ordering(verification).AfterSalesAsync(order.Id, false, default);
        Assert.Equal(75, summary.RefundedAmount);
        Assert.Equal(25, summary.RefundableAmount);
        Assert.Single(summary.Refunds);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task FullRefundClosesOnlyUnshippedRemainderAndLaterDeliveryCannotReopenOrder(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await Product(scope, "full-refund-partship", stock: 5);
        var ordering = Ordering(scope);
        var order = await Checkout(ordering, OrderPaymentMethod.Prepaid, (product.Variants.Single().Id, 3));
        order = await ordering.ConfirmPaymentAsync(order.Id, Receipt(order), default);
        var shipment = await ordering.DispatchShipmentAsync(order.Id, Dispatch(order, (order.Items.Single().Id, 1)), default);
        order = await ordering.GetOrderAsync(order.Id, false, default);
        var result = await ordering.ConfirmRefundAsync(order.Id, Refund(order), default);
        Assert.True(result.Closed);
        order = await ordering.GetOrderAsync(order.Id, false, default);
        Assert.Equal(OrderStatus.Closed, order.Status);
        Assert.Equal(OrderFulfillmentStatus.Stopped, order.FulfillmentStatus);
        Assert.Equal(OrderPaymentStatus.Paid, order.PaymentStatus);
        var balance = await db.InventoryItems.AsNoTracking().SingleAsync();
        Assert.Equal(4, balance.OnHand);
        Assert.Equal(0, balance.Reserved);
        var hold = await db.PhysicalStockReservations.AsNoTracking().SingleAsync();
        Assert.Equal(3, hold.Quantity);
        Assert.Equal(1, hold.ConsumedQuantity);
        Assert.Equal(0, hold.Remaining);
        Assert.Equal(HoldState.Closed, hold.State);
        Assert.Equal(2, await db.StockMovements.CountAsync());
        await Reject(() => ordering.DispatchShipmentAsync(order.Id, Dispatch(order, (order.Items.Single().Id, 1)), default));
        await Reject(() => ordering.RetryFulfillmentAsync(order.Id, new(order.Version), default));
        await ordering.DeliverShipmentAsync(order.Id, shipment.Id, new(order.Version, shipment.Version, Guid.NewGuid()), default);
        Assert.Equal(OrderStatus.Closed, (await ordering.GetOrderAsync(order.Id, false, default)).Status);
        Assert.Empty(await db.Set<PhysicalReturn>().AsNoTracking().ToListAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task RefundPreservesDeliveredSecretsAndPendingVerificationUntilActualResultIsRecorded(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var unique = await Product(scope, "refunded-code", ProductFulfillmentKind.DigitalUnique);
        var extension = await Product(scope, "refunded-renewal", ProductFulfillmentKind.ManualExtension);
        await Catalog(scope).ImportDigitalAsync(new(unique.Id, unique.Variants.Single().Id, unique.Version,
            [new("ONCE", "DeliveredCodeAfterRefund")]), default);
        var ordering = Ordering(scope);
        var order = await Checkout(ordering, OrderPaymentMethod.Prepaid,
            (unique.Variants.Single().Id, 1), (extension.Variants.Single().Id, 2));
        order = await ordering.ConfirmPaymentAsync(order.Id, Receipt(order), default);
        var renewal = Assert.Single(await ordering.RenewalsAsync(order.Id, default));
        renewal = await ordering.SetRenewalTargetAsync(order.Id, renewal.Id,
            new(renewal.Version, Guid.NewGuid(), "RefundPendingAccount"), default);
        var claim = await ordering.ClaimRenewalAsync(renewal.Id, new(renewal.Version, Guid.NewGuid()), default);
        var pending = await ordering.StartRenewalAsync(renewal.Id,
            new(claim.Renewal.Version, Guid.NewGuid(), claim.ClaimToken!.Value), default);
        order = await ordering.GetOrderAsync(order.Id, false, default);
        await ordering.ConfirmRefundAsync(order.Id, Refund(order), default);
        var after = await ordering.GetRenewalAdminAsync(renewal.Id, default);
        Assert.Equal(RenewalStatus.PendingVerification, after.Renewal.Status);
        Assert.Equal(pending.Renewal.Version, after.Renewal.Version);
        Assert.Equal(RenewalOutcome.Pending, Assert.Single(after.Attempts).Outcome);
        Assert.Equal(DigitalStockState.Delivered, (await db.DigitalStockItems.AsNoTracking().SingleAsync()).State);
        Assert.Equal("DeliveredCodeAfterRefund", Assert.Single(await ordering.DigitalDeliveriesAsync(order.Id, default)).Content);
        Assert.Equal("RefundPendingAccount", (await ordering.RevealRenewalTargetAsync(order.Id, renewal.Id, default)).Content);
        await Reject(() => ordering.StartRenewalAsync(renewal.Id, new(after.Renewal.Version, Guid.NewGuid(), Guid.NewGuid()), default));
        await ordering.VerifyRenewalAsync(renewal.Id, new(after.Renewal.Version, Guid.NewGuid(), after.Attempts.Single().Id,
            true, DateTime.UtcNow.AddDays(30), "Verified actual account expiry after refund.", null), default);
        Assert.Equal(RenewalStatus.Completed, (await ordering.GetRenewalAdminAsync(renewal.Id, default)).Renewal.Status);
        Assert.Equal(OrderStatus.Closed, (await ordering.GetOrderAsync(order.Id, false, default)).Status);
        Assert.Single(await db.Set<RenewalAttempt>().AsNoTracking().ToListAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task FullRefundReleasesUnstartedClaimButCannotStartProvisionOrChangeTargetOnClosedOrder(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var product = await Product(scope, "refund-before-start", ProductFulfillmentKind.ManualExtension);
        var ordering = Ordering(scope);
        var order = await Checkout(ordering, OrderPaymentMethod.Prepaid, (product.Variants.Single().Id, 1));
        order = await ordering.ConfirmPaymentAsync(order.Id, Receipt(order), default);
        var renewal = Assert.Single(await ordering.RenewalsAsync(order.Id, default));
        renewal = await ordering.SetRenewalTargetAsync(order.Id, renewal.Id,
            new(renewal.Version, Guid.NewGuid(), "UnstartedRefundAccount"), default);
        var claimed = await ordering.ClaimRenewalAsync(renewal.Id, new(renewal.Version, Guid.NewGuid()), default);
        order = await ordering.GetOrderAsync(order.Id, false, default);
        await ordering.ConfirmRefundAsync(order.Id, Refund(order), default);
        var after = await ordering.GetRenewalAdminAsync(renewal.Id, default);
        Assert.Equal(RenewalStatus.Ready, after.Renewal.Status);
        Assert.Null(after.ClaimedBy);
        Assert.Null(after.ClaimToken);
        await Reject(() => ordering.ClaimRenewalAsync(renewal.Id, new(after.Renewal.Version, Guid.NewGuid()), default));
        await Reject(() => ordering.StartRenewalAsync(renewal.Id,
            new(after.Renewal.Version, Guid.NewGuid(), claimed.ClaimToken!.Value), default));
        await Reject(() => ordering.SetRenewalTargetAsync(order.Id, renewal.Id,
            new(after.Renewal.Version, Guid.NewGuid(), "AnotherAccount"), default));
        order = await ordering.GetOrderAsync(order.Id, false, default);
        await Reject(() => ordering.ProvisionRenewalsAsync(order.Id, new(order.Version), default));
        Assert.Empty(after.Attempts);
        Assert.True((await ordering.RenewalHistoryAsync(renewal.Id, default)).Count > 3);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task FullRefundOfPaidStockReviewReleasesReservedUniqueWithoutCreatingADelivery(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var unique = await Product(scope, "refund-reserved-code", ProductFulfillmentKind.DigitalUnique);
        var shared = await Product(scope, "refund-disabled-shared", ProductFulfillmentKind.DigitalShared);
        await Catalog(scope).ImportDigitalAsync(new(unique.Id, unique.Variants.Single().Id, unique.Version,
            [new("RESERVED", "MustNotDeliverAfterRefund")]), default);
        var sharedStock = await Catalog(scope).SaveSharedAsync(new(shared.Id, shared.Variants.Single().Id, shared.Version,
            "https://example.test/course"), default);
        var ordering = Ordering(scope);
        var order = await Checkout(ordering, OrderPaymentMethod.Prepaid,
            (unique.Variants.Single().Id, 1), (shared.Variants.Single().Id, 1));
        await Catalog(scope).SetDigitalStockAvailabilityAsync(new(shared.Id, shared.Variants.Single().Id, sharedStock.Id,
            sharedStock.ProductVersion, false), default);
        order = await ordering.ConfirmPaymentAsync(order.Id, Receipt(order), default);
        Assert.Equal(OrderFulfillmentStatus.NeedsReview, order.FulfillmentStatus);
        Assert.Equal(DigitalStockState.Reserved,
            (await db.DigitalStockItems.AsNoTracking().SingleAsync(i => i.FulfillmentKind == ProductFulfillmentKind.DigitalUnique)).State);
        await ordering.ConfirmRefundAsync(order.Id, Refund(order), default);
        var released = await db.DigitalStockItems.AsNoTracking().SingleAsync(i => i.FulfillmentKind == ProductFulfillmentKind.DigitalUnique);
        Assert.Equal(DigitalStockState.Available, released.State);
        Assert.Null(released.ReservedForOrderItemId);
        Assert.Equal(DigitalStockState.Disabled,
            (await db.DigitalStockItems.AsNoTracking().SingleAsync(i => i.FulfillmentKind == ProductFulfillmentKind.DigitalShared)).State);
        Assert.Empty(await db.DigitalDeliveries.AsNoTracking().ToListAsync());
        Assert.Empty(await ordering.DigitalDeliveriesAsync(order.Id, default));
        Assert.Equal(OrderStatus.Closed, (await ordering.GetOrderAsync(order.Id, false, default)).Status);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ReturnsRestockOnlyInspectedQuantityAgainstOriginalDispatchBalanceWithoutImplyingRefund(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await Product(scope, "partial-return", stock: 5);
        var ordering = Ordering(scope);
        var order = await Checkout(ordering, OrderPaymentMethod.Prepaid, (product.Variants.Single().Id, 3));
        order = await ordering.ConfirmPaymentAsync(order.Id, Receipt(order), default);
        var shipment = await ordering.DispatchShipmentAsync(order.Id, Dispatch(order, (order.Items.Single().Id, 3)), default);
        order = await ordering.GetOrderAsync(order.Id, false, default);
        var first = await ordering.ReceiveReturnAsync(order.Id, Return(order, shipment, 2, 1), default);
        Assert.False(first.Closed);
        order = await ordering.GetOrderAsync(order.Id, false, default);
        await ordering.ReceiveReturnAsync(order.Id, Return(order, shipment, 1, 0), default);
        var balance = await db.InventoryItems.AsNoTracking().SingleAsync();
        Assert.Equal(3, balance.OnHand);
        Assert.Equal(0, balance.Reserved);
        var holds = await db.PhysicalStockReservations.AsNoTracking().SingleAsync();
        Assert.Equal(3, holds.ConsumedQuantity);
        Assert.Equal(HoldState.Consumed, holds.State);
        var original = await db.Set<ShipmentItem>().AsNoTracking().SingleAsync();
        var dispatch = await db.StockMovements.AsNoTracking().SingleAsync(m => m.Id == original.StockMovementId);
        var restock = await db.StockMovements.AsNoTracking().SingleAsync(m => m.Delta == 1);
        Assert.Equal(dispatch.InventoryItemId, restock.InventoryItemId);
        Assert.Equal(3, restock.QuantityAfter);
        var summary = await ordering.AfterSalesAsync(order.Id, false, default);
        Assert.Equal(2, summary.Returns.Count);
        Assert.Equal(3, summary.Returns.SelectMany(r => r.Items).Sum(i => i.Quantity));
        Assert.Equal(1, summary.Returns.SelectMany(r => r.Items).Sum(i => i.RestockQuantity));
        Assert.Empty(summary.Refunds);
        Assert.Equal(0, summary.RefundedAmount);
        Assert.Equal(300, summary.RefundableAmount);
        Assert.Equal(ShipmentStatus.Dispatched, Assert.Single(await ordering.ShipmentsAsync(order.Id, false, default)).Status);
        order = await ordering.GetOrderAsync(order.Id, false, default);
        await Reject(() => ordering.ReceiveReturnAsync(order.Id, Return(order, shipment, 1, 0), default));
        Assert.Equal(2, await db.Set<PhysicalReturn>().CountAsync());
        Assert.Equal(3, await db.StockMovements.CountAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ReturnRejectsWrongOrderDigitalItemDuplicateLinesAndInvalidRestockWithoutSideEffects(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var physical = await Product(scope, "return-validation");
        var extension = await Product(scope, "return-digital", ProductFulfillmentKind.ManualExtension);
        var ordering = Ordering(scope);
        var order = await Checkout(ordering, OrderPaymentMethod.Prepaid,
            (physical.Variants.Single().Id, 2), (extension.Variants.Single().Id, 1));
        order = await ordering.ConfirmPaymentAsync(order.Id, Receipt(order), default);
        var item = order.Items.Single(i => i.FulfillmentKind == ProductFulfillmentKind.Physical);
        var digital = order.Items.Single(i => i.FulfillmentKind == ProductFulfillmentKind.ManualExtension);
        var shipment = await ordering.DispatchShipmentAsync(order.Id, Dispatch(order, (item.Id, 2)), default);
        order = await ordering.GetOrderAsync(order.Id, false, default);
        var command = Return(order, shipment, 1, 1);
        await Reject(() => ordering.ReceiveReturnAsync(order.Id, command with { Items = [new(shipment.Id, digital.Id, 1, 0)] }, default));
        await Reject(() => ordering.ReceiveReturnAsync(order.Id, command with { Items = [new(Guid.NewGuid(), item.Id, 1, 0)] }, default));
        await Reject(() => ordering.ReceiveReturnAsync(order.Id, command with { Items = [new(shipment.Id, item.Id, 1, 2)] }, default));
        await Reject(() => ordering.ReceiveReturnAsync(order.Id, command with { Items = [new(shipment.Id, item.Id, 1, -1)] }, default));
        await Reject(() => ordering.ReceiveReturnAsync(order.Id, command with { Items = [command.Items[0], command.Items[0]] }, default));
        await Reject(() => ordering.ReceiveReturnAsync(order.Id, command with { Items = [] }, default));
        await Reject(() => ordering.ReceiveReturnAsync(order.Id, command with { CloseRemainingCod = true }, default));
        var another = await Checkout(ordering, OrderPaymentMethod.CashOnDelivery, (physical.Variants.Single().Id, 1));
        await Reject(() => ordering.ReceiveReturnAsync(another.Id, command with { OrderVersion = another.Version }, default));
        Assert.Empty(await db.Set<PhysicalReturn>().AsNoTracking().ToListAsync());
        Assert.Equal(2, await db.StockMovements.CountAsync());
        Assert.Equal(8, (await db.InventoryItems.AsNoTracking().SingleAsync(i => i.VariantId == physical.Variants.Single().Id)).OnHand);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ReturnReplayReferenceAndRestockPermissionCannotBypassIndependentAuthorization(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await Product(scope, "return-acl");
        var ordering = Ordering(scope);
        var order = await Checkout(ordering, OrderPaymentMethod.CashOnDelivery, (product.Variants.Single().Id, 2));
        var shipment = await ordering.DispatchShipmentAsync(order.Id, Dispatch(order, (order.Items.Single().Id, 2)), default);
        order = await ordering.GetOrderAsync(order.Id, false, default);
        var command = Return(order, shipment, 1, 1);
        await Assert.ThrowsAsync<ForbiddenException>(() => Ordering(scope, "Permissions.Return.Receive").ReceiveReturnAsync(order.Id, command, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => Ordering(scope, "Permissions.Inventory.Adjust").ReceiveReturnAsync(order.Id, command, default));
        Assert.Empty(await db.Set<PhysicalReturn>().AsNoTracking().ToListAsync());
        var received = await ordering.ReceiveReturnAsync(order.Id, command, default);
        Assert.Equal(received.Return.Id, (await ordering.ReceiveReturnAsync(order.Id,
            command with { OrderVersion = "lost-response" }, default)).Return.Id);
        await Assert.ThrowsAsync<ForbiddenException>(() => Ordering(scope, "Permissions.Inventory.Adjust").ReceiveReturnAsync(order.Id, command, default));
        using var other = fixture.Scope(Guid.NewGuid());
        await Assert.ThrowsAsync<ConflictException>(() => Ordering(other).ReceiveReturnAsync(order.Id, command, default));
        await Assert.ThrowsAsync<ConflictException>(() => ordering.ReceiveReturnAsync(order.Id,
            command with { Items = [new(shipment.Id, order.Items.Single().Id, 1, 0)] }, default));
        order = await ordering.GetOrderAsync(order.Id, false, default);
        await Assert.ThrowsAsync<ConflictException>(() => ordering.ReceiveReturnAsync(order.Id,
            command with { OperationId = Guid.NewGuid(), OrderVersion = order.Version, Reference = command.Reference.ToLowerInvariant() }, default));
        // No restock is a valid receipt-only action and does not require Inventory.Adjust.
        await Ordering(scope, "Permissions.Inventory.Adjust").ReceiveReturnAsync(order.Id, Return(order, shipment, 1, 0), default);
        Assert.Equal(2, await db.Set<PhysicalReturn>().CountAsync());
        Assert.Equal(3, await db.StockMovements.CountAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task PhysicalReturnAfterFullRefundRecordsGoodsAndCannotInventAnotherRefundOrReopenOrder(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var product = await Product(scope, "return-after-refund", stock: 2);
        var ordering = Ordering(scope);
        var order = await Checkout(ordering, OrderPaymentMethod.Prepaid, (product.Variants.Single().Id, 2));
        order = await ordering.ConfirmPaymentAsync(order.Id, Receipt(order), default);
        var shipment = await ordering.DispatchShipmentAsync(order.Id, Dispatch(order, (order.Items.Single().Id, 2)), default);
        order = await ordering.GetOrderAsync(order.Id, false, default);
        await ordering.ConfirmRefundAsync(order.Id, Refund(order), default);
        order = await ordering.GetOrderAsync(order.Id, false, default);
        var received = await ordering.ReceiveReturnAsync(order.Id, Return(order, shipment, 2, 2), default);
        Assert.True(received.Closed);
        var summary = await ordering.AfterSalesAsync(order.Id, false, default);
        Assert.True(summary.Closed);
        Assert.Single(summary.Refunds);
        Assert.Single(summary.Returns);
        Assert.Equal(200, summary.RefundedAmount);
        Assert.Equal(0, summary.RefundableAmount);
        Assert.Equal(OrderStatus.Closed, (await ordering.GetOrderAsync(order.Id, false, default)).Status);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(2, (await db.InventoryItems.AsNoTracking().SingleAsync()).OnHand);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task UnpaidCodRequiresExplicitFinalReturnToCloseAndLateBankReceiptCannotRestartFulfillment(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await Product(scope, "cod-return", stock: 5);
        var ordering = Ordering(scope);
        var order = await Checkout(ordering, OrderPaymentMethod.CashOnDelivery, (product.Variants.Single().Id, 3));
        var shipment = await ordering.DispatchShipmentAsync(order.Id, Dispatch(order, (order.Items.Single().Id, 2)), default);
        order = await ordering.GetOrderAsync(order.Id, false, default);
        await Reject(() => ordering.ReceiveReturnAsync(order.Id, Return(order, shipment, 1, 1, true), default));
        Assert.Empty(await db.Set<PhysicalReturn>().AsNoTracking().ToListAsync());
        await ordering.ReceiveReturnAsync(order.Id, Return(order, shipment, 1, 1), default);
        order = await ordering.GetOrderAsync(order.Id, false, default);
        Assert.Equal(OrderStatus.AwaitingProcessing, order.Status);
        var final = await ordering.ReceiveReturnAsync(order.Id, Return(order, shipment, 1, 0, true), default);
        Assert.True(final.Closed);
        order = await ordering.GetOrderAsync(order.Id, false, default);
        Assert.Equal(OrderPaymentStatus.Unpaid, order.PaymentStatus);
        Assert.Equal(OrderStatus.Closed, order.Status);
        Assert.Equal(OrderFulfillmentStatus.Stopped, order.FulfillmentStatus);
        Assert.Equal(0, (await db.InventoryItems.AsNoTracking().SingleAsync()).Reserved);
        Assert.Equal(4, (await db.InventoryItems.AsNoTracking().SingleAsync()).OnHand);
        Assert.Equal(HoldState.Closed, (await db.PhysicalStockReservations.AsNoTracking().SingleAsync()).State);
        Assert.Empty((await ordering.AfterSalesAsync(order.Id, false, default)).Refunds);
        await Reject(() => ordering.ConfirmPaymentAsync(order.Id, Receipt(order) with { Source = "ManualCOD" }, default));
        var receipt = Receipt(order);
        var paid = await ordering.ConfirmPaymentAsync(order.Id, receipt, default);
        Assert.Equal(OrderPaymentStatus.Paid, paid.PaymentStatus);
        Assert.Equal(OrderStatus.Closed, paid.Status);
        Assert.Equal(OrderFulfillmentStatus.Stopped, paid.FulfillmentStatus);
        Assert.Single(await db.PaymentReceipts.AsNoTracking().ToListAsync());
        Assert.Single(await db.Set<Shipment>().AsNoTracking().ToListAsync());
        await ordering.ConfirmPaymentAsync(order.Id, receipt, default);
        Assert.Single(await db.PaymentReceipts.AsNoTracking().ToListAsync());
        var refund = await ordering.ConfirmRefundAsync(order.Id, Refund(paid), default);
        Assert.True(refund.Closed);
        Assert.Equal(0, (await ordering.AfterSalesAsync(order.Id, false, default)).RefundableAmount);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ConcurrentReturnsCannotReceiveOrRestockMoreThanTheShipment(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var owner = Guid.NewGuid();
        OrderDto order;
        ShipmentDto shipment;
        using (var scope = fixture.Scope(owner))
        {
            var product = await Product(scope, "return-race", stock: 1);
            var ordering = Ordering(scope);
            order = await Checkout(ordering, OrderPaymentMethod.CashOnDelivery, (product.Variants.Single().Id, 1));
            shipment = await ordering.DispatchShipmentAsync(order.Id, Dispatch(order, (order.Items.Single().Id, 1)), default);
            order = await ordering.GetOrderAsync(order.Id, false, default);
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<ReturnResult?> Attempt()
        {
            using var scope = fixture.Scope(Guid.NewGuid());
            await start.Task;
            try { return await Ordering(scope).ReceiveReturnAsync(order.Id, Return(order, shipment, 1, 1), timeout.Token); }
            catch (ConflictException) { return null; }
        }
        var first = Attempt();
        var second = Attempt();
        start.SetResult();
        Assert.Single((await Task.WhenAll(first, second)).Where(i => i is not null));
        using var verification = fixture.Scope(owner);
        var db = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Single(await db.Set<PhysicalReturn>().AsNoTracking().ToListAsync());
        Assert.Equal(1, (await db.InventoryItems.AsNoTracking().SingleAsync()).OnHand);
        Assert.Equal(2, await db.StockMovements.CountAsync(m => m.Delta == 1));
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task RefundCommitFailureRollsBackLedgerClosureReservationAndAuditThenSameOperationCanRetry(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await Product(scope, "refund-rollback", stock: 3);
        var ordering = Ordering(scope);
        var order = await Checkout(ordering, OrderPaymentMethod.Prepaid, (product.Variants.Single().Id, 2));
        order = await ordering.ConfirmPaymentAsync(order.Id, Receipt(order), default);
        var audit = await db.Trails.CountAsync();
        var command = Refund(order);
        var failure = new CommitFailure();
        await using (var failing = FailingContext(scope, failure))
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Ordering(scope, dbOverride: failing).ConfirmRefundAsync(order.Id, command, default));
            Assert.Same(failure.Error, error);
            Assert.True(failure.WasReached);
            Assert.Empty(failing.ChangeTracker.Entries());
        }
        Assert.Empty(await db.Set<Refund>().AsNoTracking().ToListAsync());
        Assert.Equal(audit, await db.Trails.CountAsync());
        Assert.Equal(order.Version, (await ordering.GetOrderAsync(order.Id, false, default)).Version);
        Assert.Equal(HoldState.Held, (await db.PhysicalStockReservations.AsNoTracking().SingleAsync()).State);
        Assert.Equal(2, (await db.InventoryItems.AsNoTracking().SingleAsync()).Reserved);
        Assert.True((await ordering.ConfirmRefundAsync(order.Id, command, default)).Closed);
        await ordering.ConfirmRefundAsync(order.Id, command, default);
        Assert.Single(await db.Set<Refund>().AsNoTracking().ToListAsync());
        Assert.Equal(0, (await db.InventoryItems.AsNoTracking().SingleAsync()).Reserved);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ReturnCommitFailureRollsBackReceiptRestockCodClosureAndAuditBeforeSafeRetry(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await Product(scope, "return-rollback", stock: 3);
        var ordering = Ordering(scope);
        var order = await Checkout(ordering, OrderPaymentMethod.CashOnDelivery, (product.Variants.Single().Id, 2));
        var shipment = await ordering.DispatchShipmentAsync(order.Id, Dispatch(order, (order.Items.Single().Id, 1)), default);
        order = await ordering.GetOrderAsync(order.Id, false, default);
        var audit = await db.Trails.CountAsync();
        var command = Return(order, shipment, 1, 1, true);
        var failure = new CommitFailure();
        await using (var failing = FailingContext(scope, failure))
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Ordering(scope, dbOverride: failing).ReceiveReturnAsync(order.Id, command, default));
            Assert.Same(failure.Error, error);
            Assert.True(failure.WasReached);
            Assert.Empty(failing.ChangeTracker.Entries());
        }
        Assert.Empty(await db.Set<PhysicalReturn>().AsNoTracking().ToListAsync());
        Assert.Equal(2, await db.StockMovements.CountAsync());
        Assert.Equal(audit, await db.Trails.CountAsync());
        Assert.Equal(order.Version, (await ordering.GetOrderAsync(order.Id, false, default)).Version);
        Assert.Equal(2, (await db.InventoryItems.AsNoTracking().SingleAsync()).OnHand);
        Assert.Equal(1, (await db.InventoryItems.AsNoTracking().SingleAsync()).Reserved);
        Assert.True((await ordering.ReceiveReturnAsync(order.Id, command, default)).Closed);
        await ordering.ReceiveReturnAsync(order.Id, command, default);
        Assert.Single(await db.Set<PhysicalReturn>().AsNoTracking().ToListAsync());
        Assert.Single(await db.Set<PhysicalReturnItem>().AsNoTracking().ToListAsync());
        Assert.Equal(3, (await db.InventoryItems.AsNoTracking().SingleAsync()).OnHand);
        Assert.Equal(0, (await db.InventoryItems.AsNoTracking().SingleAsync()).Reserved);
        Assert.Equal(3, await db.StockMovements.CountAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task HttpAfterSalesRequiresAuthenticationOwnershipSeparatePermissionsAndNoStore(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var oldEmail = Environment.GetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL");
        var oldPassword = Environment.GetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD");
        try
        {
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL", "aftersales@example.test");
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD", "AfterSalesPassword!29874");
            using var bootstrap = fixture.Scope();
            await bootstrap.ServiceProvider.GetRequiredService<FoundationBootstrap>().RunAsync(["--bootstrap-admin"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL", oldEmail);
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD", oldPassword);
        }
        Guid owner;
        ProductDto product;
        using (var setup = fixture.Scope(Guid.NewGuid()))
        {
            var db = setup.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            owner = Guid.Parse(await db.Users.Where(u => u.Email == "aftersales@example.test").Select(u => u.Id).SingleAsync());
            product = await Product(setup, "aftersales-http");
        }
        OrderDto mine;
        using (var scope = fixture.Scope(owner))
        {
            var ordering = Ordering(scope);
            mine = await Checkout(ordering, OrderPaymentMethod.Prepaid, (product.Variants.Single().Id, 1));
            mine = await ordering.ConfirmPaymentAsync(mine.Id, Receipt(mine), default);
        }
        OrderDto another;
        using (var scope = fixture.Scope(Guid.NewGuid()))
            another = await Checkout(Ordering(scope), OrderPaymentMethod.CashOnDelivery, (product.Variants.Single().Id, 1));
        await using var app = await TestHostApplication.StartAsync(fixture, true);
        using var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/orders/{mine.Id}/after-sales")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync($"/api/admin/orders/{mine.Id}/refunds", Refund(mine))).StatusCode);
        var login = await client.PostAsJsonAsync("/api/tokens/get", new TokenRequest("aftersales@example.test", "AfterSalesPassword!29874"));
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", token.Token);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/orders/{another.Id}/after-sales")).StatusCode);
        var summary = await client.GetAsync($"/api/orders/{mine.Id}/after-sales");
        summary.EnsureSuccessStatusCode();
        Assert.True(summary.Headers.CacheControl?.NoStore);
        Assert.Equal(100, (await summary.Content.ReadFromJsonAsync<AfterSalesDto>())!.RefundableAmount);
        var response = await client.PostAsJsonAsync($"/api/admin/orders/{mine.Id}/refunds", Refund(mine, 25));
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        using (var scope = fixture.Scope(owner))
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var function = await db.Functions.Where(f => f.Code == "Refund").Select(f => f.Id).SingleAsync();
            var action = await db.Actions.Where(a => a.Code == "Confirm").Select(a => a.Id).SingleAsync();
            await db.Permissions.Where(p => p.FunctionId == function && p.ActionId == action)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.DeletedOn, DateTime.UtcNow));
            mine = await Ordering(scope).GetOrderAsync(mine.Id, false, default);
        }
        response = await client.PostAsJsonAsync($"/api/admin/orders/{mine.Id}/refunds", Refund(mine, 25));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        summary = await client.GetAsync($"/api/admin/orders/{mine.Id}/after-sales");
        summary.EnsureSuccessStatusCode();
        Assert.True(summary.Headers.CacheControl?.NoStore);
        Assert.Equal(25, (await summary.Content.ReadFromJsonAsync<AfterSalesDto>())!.RefundedAmount);
    }
}
