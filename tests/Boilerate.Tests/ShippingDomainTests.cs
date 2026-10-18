using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.Inventory;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Payments;
using Boilerate.Domain.Shipping;
using Xunit;
namespace Boilerate.Tests;

public sealed class ShippingDomainTests
{
    private static readonly DateTime Now = new(2026, 10, 18, 11, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void DispatchKeepsItsOrderAndMovementEvidenceAndNormalizesCarrierFields()
    {
        var orderId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var first = Item(orderId, 2);
        var second = Item(orderId, 1);
        var shipment = new Shipment(orderId, operationId, new string('a', 64), "  Local courier  ", " TRACK-1 ", Now, [first, second]);
        Assert.Equal(orderId, shipment.OrderId);
        Assert.Equal(operationId, shipment.OperationId);
        Assert.Equal(new string('a', 64), shipment.RequestHash);
        Assert.Equal("Local courier", shipment.Carrier);
        Assert.Equal("TRACK-1", shipment.TrackingNumber);
        Assert.Equal(ShipmentStatus.Dispatched, shipment.Status);
        Assert.Equal(Now, shipment.DispatchedAtUtc);
        Assert.Null(shipment.DeliveredAtUtc);
        Assert.Null(shipment.DeliveryOperationId);
        Assert.Equal(2, shipment.Items.Count);
        Assert.All(shipment.Items, item =>
        {
            Assert.Equal(shipment.Id, item.ShipmentId);
            Assert.Equal(orderId, item.OrderId);
            Assert.Equal(ProductFulfillmentKind.Physical, item.FulfillmentKind);
            Assert.NotEqual(Guid.Empty, item.StockMovementId);
        });
        Assert.Equal(2, first.Quantity);
    }

    [Fact]
    public void DispatchAndDeliveryPreserveMicrosecondsAcrossDatabaseRoundTrips()
    {
        var shipment = NewShipment(now: Now.AddTicks(1234567));
        Assert.Equal(Now.AddTicks(1234560), shipment.DispatchedAtUtc);
        shipment.MarkDelivered(Guid.NewGuid(), Now.AddTicks(2345678));
        Assert.Equal(Now.AddTicks(2345670), shipment.DeliveredAtUtc);
        Assert.Equal(DateTimeKind.Utc, shipment.DispatchedAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, shipment.DeliveredAtUtc!.Value.Kind);
    }

    [Fact]
    public void ShipmentRejectsMissingIdsMalformedHashAndNonUtcDispatchTime()
    {
        Assert.Throws<DomainException>(() => NewShipment(orderId: Guid.Empty));
        Assert.Throws<DomainException>(() => NewShipment(operationId: Guid.Empty));
        Assert.Throws<DomainException>(() => NewShipment(hash: null!));
        Assert.Throws<DomainException>(() => NewShipment(hash: "bad"));
        Assert.Throws<DomainException>(() => NewShipment(hash: new string('g', 64)));
        Assert.Throws<DomainException>(() => NewShipment(now: DateTime.SpecifyKind(Now, DateTimeKind.Unspecified)));
        Assert.Throws<DomainException>(() => NewShipment(now: DateTime.SpecifyKind(Now, DateTimeKind.Local)));
    }

    [Fact]
    public void ShipmentValidatesAllInputsBeforeAttachingAnyItem()
    {
        var orderId = Guid.NewGuid();
        var first = Item(orderId);
        var validItems = new[] { first };
        Assert.Throws<DomainException>(() => NewShipment(orderId, carrier: " ", items: validItems));
        Assert.Throws<DomainException>(() => NewShipment(orderId, carrier: new string('x', 101), items: validItems));
        Assert.Throws<DomainException>(() => NewShipment(orderId, tracking: " ", items: validItems));
        Assert.Throws<DomainException>(() => NewShipment(orderId, tracking: new string('x', 101), items: validItems));
        Assert.Throws<DomainException>(() => NewShipment(orderId, items: [first, Item(Guid.NewGuid())]));
        Assert.Throws<DomainException>(() => NewShipment(orderId, items: [first, null!]));
        var duplicate = new ShipmentItem(orderId, first.OrderItemId, first.VariantId, Guid.NewGuid(), 1);
        Assert.Throws<DomainException>(() => NewShipment(orderId, items: [first, duplicate]));
        Assert.Equal(Guid.Empty, first.ShipmentId);
        Assert.Equal(Guid.Empty, duplicate.ShipmentId);
        var shipment = NewShipment(orderId, items: validItems);
        Assert.Throws<DomainException>(() => NewShipment(orderId, items: validItems));
        Assert.Equal(shipment.Id, first.ShipmentId);
    }

    [Fact]
    public void ShipmentRequiresOneToFiftyDistinctOrderLines()
    {
        var orderId = Guid.NewGuid();
        Assert.Throws<DomainException>(() => new Shipment(orderId, Guid.NewGuid(), new string('a', 64), "Carrier", "Tracking", Now, null!));
        Assert.Throws<DomainException>(() => NewShipment(orderId, items: []));
        var oversized = Enumerable.Range(0, 51).Select(_ => Item(orderId)).ToArray();
        Assert.Throws<DomainException>(() => NewShipment(orderId, items: oversized));
        Assert.All(oversized, item => Assert.Equal(Guid.Empty, item.ShipmentId));
        Assert.Equal(50, NewShipment(orderId, items: oversized.Take(50).ToArray()).Items.Count);
    }

    [Fact]
    public void ShipmentLineRequiresAllLinksAndPositiveBoundedQuantity()
    {
        var ids = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        for (var index = 0; index < ids.Length; index++)
        {
            var invalid = ids.ToArray();
            invalid[index] = Guid.Empty;
            Assert.Throws<DomainException>(() => new ShipmentItem(invalid[0], invalid[1], invalid[2], invalid[3], 1));
        }
        Assert.Throws<DomainException>(() => Item(Guid.NewGuid(), 0));
        Assert.Throws<DomainException>(() => Item(Guid.NewGuid(), 1000));
        Assert.Equal(999, Item(Guid.NewGuid(), 999).Quantity);
    }

    [Fact]
    public void DeliveryIsIdempotentOnlyForTheOriginalOperationAndKeepsItsFirstTime()
    {
        var shipment = NewShipment();
        var operation = Guid.NewGuid();
        var version = shipment.Version;
        Assert.True(shipment.MarkDelivered(operation, Now.AddMinutes(1)));
        Assert.Equal(ShipmentStatus.Delivered, shipment.Status);
        Assert.Equal(operation, shipment.DeliveryOperationId);
        Assert.Equal(Now.AddMinutes(1), shipment.DeliveredAtUtc);
        Assert.NotEqual(version, shipment.Version);
        version = shipment.Version;
        Assert.False(shipment.MarkDelivered(operation, Now.AddHours(1)));
        Assert.Equal(Now.AddMinutes(1), shipment.DeliveredAtUtc);
        Assert.Equal(version, shipment.Version);
        Assert.Throws<DomainException>(() => shipment.MarkDelivered(Guid.NewGuid(), Now.AddHours(1)));
        Assert.Equal(version, shipment.Version);
    }

    [Fact]
    public void InvalidDeliveryEvidenceDoesNotMutateTheShipment()
    {
        var shipment = NewShipment();
        var version = shipment.Version;
        Assert.Throws<DomainException>(() => shipment.MarkDelivered(Guid.Empty, Now));
        Assert.Throws<DomainException>(() => shipment.MarkDelivered(Guid.NewGuid(), Now.AddTicks(-1)));
        Assert.Throws<DomainException>(() => shipment.MarkDelivered(Guid.NewGuid(), DateTime.SpecifyKind(Now, DateTimeKind.Unspecified)));
        Assert.Throws<DomainException>(() => shipment.MarkDelivered(Guid.NewGuid(), DateTime.SpecifyKind(Now, DateTimeKind.Local)));
        Assert.Equal(ShipmentStatus.Dispatched, shipment.Status);
        Assert.Null(shipment.DeliveredAtUtc);
        Assert.Null(shipment.DeliveryOperationId);
        Assert.Equal(version, shipment.Version);
        Assert.True(shipment.MarkDelivered(Guid.NewGuid(), Now));
    }

    [Fact]
    public void PhysicalReservationSupportsPartialAndFinalConsumptionWithoutChangingTheOriginalQuantity()
    {
        var hold = Reservation(3);
        var version = hold.Version;
        hold.Consume(1);
        Assert.Equal(1, hold.ConsumedQuantity);
        Assert.Equal(2, hold.Remaining);
        Assert.Equal(3, hold.Quantity);
        Assert.Equal(HoldState.Held, hold.State);
        Assert.NotEqual(version, hold.Version);
        version = hold.Version;
        Assert.Throws<DomainException>(() => hold.Consume(3));
        Assert.Equal(version, hold.Version);
        Assert.Equal(1, hold.ConsumedQuantity);
        hold.Consume(2);
        Assert.Equal(3, hold.Quantity);
        Assert.Equal(3, hold.ConsumedQuantity);
        Assert.Equal(0, hold.Remaining);
        Assert.Equal(HoldState.Consumed, hold.State);
        version = hold.Version;
        Assert.Throws<DomainException>(() => hold.Consume(1));
        Assert.Equal(version, hold.Version);
    }

    [Fact]
    public void AnyDispatchBlocksLegacyReservationReleaseAndReacquire()
    {
        var hold = Reservation(2);
        hold.Consume(1);
        var version = hold.Version;
        Assert.Throws<DomainException>(() => hold.Release());
        Assert.Throws<DomainException>(() => hold.Reacquire());
        Assert.Equal(version, hold.Version);
        Assert.Equal(HoldState.Held, hold.State);
        hold.Consume(1);
        version = hold.Version;
        Assert.Throws<DomainException>(() => hold.Release());
        Assert.Throws<DomainException>(() => hold.Reacquire());
        Assert.Equal(version, hold.Version);
        Assert.Equal(HoldState.Consumed, hold.State);
    }

    [Fact]
    public void ReleasedOrInvalidReservationCannotBeConsumed()
    {
        var hold = Reservation(2);
        var version = hold.Version;
        Assert.Throws<DomainException>(() => hold.Consume(0));
        Assert.Throws<DomainException>(() => hold.Consume(-1));
        Assert.Throws<DomainException>(() => hold.Consume(3));
        Assert.Equal(version, hold.Version);
        Assert.True(hold.Release());
        version = hold.Version;
        Assert.Throws<DomainException>(() => hold.Consume(1));
        Assert.Equal(version, hold.Version);
        Assert.Equal(0, hold.ConsumedQuantity);
        Assert.True(hold.Reacquire());
        hold.Consume(2);
        Assert.Equal(HoldState.Consumed, hold.State);
    }

    [Fact]
    public void ConsumingReservedStockCreatesANegativeLedgerEntryAndKeepsOtherAvailability()
    {
        var balance = Balance(5, 3);
        var operation = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var version = balance.Version;
        var movement = balance.ConsumeReserved(operation, 2, "  Shipment dispatch  ", actor);
        Assert.Equal(3, balance.OnHand);
        Assert.Equal(1, balance.Reserved);
        Assert.Equal(2, balance.Available);
        Assert.NotEqual(version, balance.Version);
        Assert.Equal(balance.Id, movement.InventoryItemId);
        Assert.Equal(operation, movement.OperationId);
        Assert.Equal(actor, movement.ActorId);
        Assert.Equal(-2, movement.Delta);
        Assert.Equal(3, movement.QuantityAfter);
        Assert.Equal("Shipment dispatch", movement.Reason);
        balance.ConsumeReserved(Guid.NewGuid(), 1, "Final held item", actor);
        Assert.Equal(2, balance.OnHand);
        Assert.Equal(0, balance.Reserved);
        Assert.Equal(2, balance.Available);
    }

    [Fact]
    public void InvalidConsumptionLeavesBalanceAndVersionUnchanged()
    {
        var balance = Balance(5, 2);
        var version = balance.Version;
        Assert.Throws<DomainException>(() => balance.ConsumeReserved(Guid.Empty, 1, "Dispatch", Guid.NewGuid()));
        Assert.Throws<DomainException>(() => balance.ConsumeReserved(Guid.NewGuid(), 1, "Dispatch", Guid.Empty));
        Assert.Throws<DomainException>(() => balance.ConsumeReserved(Guid.NewGuid(), 0, "Dispatch", Guid.NewGuid()));
        Assert.Throws<DomainException>(() => balance.ConsumeReserved(Guid.NewGuid(), -1, "Dispatch", Guid.NewGuid()));
        Assert.Throws<DomainException>(() => balance.ConsumeReserved(Guid.NewGuid(), 3, "Dispatch", Guid.NewGuid()));
        Assert.Throws<DomainException>(() => balance.ConsumeReserved(Guid.NewGuid(), 1, " ", Guid.NewGuid()));
        Assert.Throws<DomainException>(() => balance.ConsumeReserved(Guid.NewGuid(), 1, new string('x', 1001), Guid.NewGuid()));
        Assert.Equal(5, balance.OnHand);
        Assert.Equal(2, balance.Reserved);
        Assert.Equal(version, balance.Version);
        var unreserved = Balance(5, 0);
        Assert.Throws<DomainException>(() => unreserved.ConsumeReserved(Guid.NewGuid(), 1, "Not held", Guid.NewGuid()));
    }

    [Fact]
    public void UnpaidPrepaidCancelledExpiredAndDigitalOnlyOrdersCannotDispatch()
    {
        var unpaid = NewOrder();
        var version = unpaid.Version;
        Assert.Throws<DomainException>(() => unpaid.RegisterPhysicalShipment(false));
        Assert.Equal(version, unpaid.Version);
        Assert.False(unpaid.PhysicalDispatchStarted);
        var cancelled = NewOrder(OrderPaymentMethod.CashOnDelivery);
        cancelled.Cancel();
        Assert.Throws<DomainException>(() => cancelled.RegisterPhysicalShipment(false));
        var expired = NewOrder();
        expired.Expire(Now.AddMinutes(30));
        Assert.Throws<DomainException>(() => expired.RegisterPhysicalShipment(false));
        var digital = NewOrder(items: [OrderLine(ProductFulfillmentKind.DigitalUnique)]);
        digital.RecordPayment(Now);
        Assert.Throws<DomainException>(() => digital.RegisterPhysicalShipment(true));
    }

    [Fact]
    public void PaidOrderRecordsPartialAndCompleteShippingAndCannotBeCancelled()
    {
        var order = NewOrder();
        order.RecordPayment(Now);
        order.CompleteFulfillment(false);
        var version = order.Version;
        order.RegisterPhysicalShipment(false);
        Assert.True(order.PhysicalDispatchStarted);
        Assert.Equal(OrderStatus.AwaitingProcessing, order.Status);
        Assert.Equal(OrderFulfillmentStatus.InProgress, order.FulfillmentStatus);
        Assert.NotEqual(version, order.Version);
        Assert.Throws<DomainException>(order.Cancel);
        order.RegisterPhysicalShipment(true);
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal(OrderPaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal(OrderFulfillmentStatus.Completed, order.FulfillmentStatus);
    }

    [Fact]
    public void DeliveredCodRemainsUnpaidUntilActualReceiptAndCannotBeCancelledAfterDispatch()
    {
        var order = NewOrder(OrderPaymentMethod.CashOnDelivery);
        order.RegisterPhysicalShipment(false);
        Assert.True(order.PhysicalDispatchStarted);
        Assert.Throws<DomainException>(order.Cancel);
        order.RegisterPhysicalShipment(true);
        Assert.Equal(OrderFulfillmentStatus.Completed, order.FulfillmentStatus);
        Assert.Equal(OrderPaymentStatus.Unpaid, order.PaymentStatus);
        Assert.Equal(OrderStatus.AwaitingProcessing, order.Status);
        Assert.Null(order.PaidAtUtc);
        order.RecordPayment(Now);
        order.RegisterPhysicalShipment(true);
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal(OrderPaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal(OrderFulfillmentStatus.Completed, order.FulfillmentStatus);
    }

    [Fact]
    public void ShippingDoesNotPresumeThatManualExtensionInAMixedOrderHasFinished()
    {
        var order = NewOrder(items: [OrderLine(ProductFulfillmentKind.Physical), OrderLine(ProductFulfillmentKind.ManualExtension)]);
        order.RecordPayment(Now);
        order.CompleteFulfillment(false);
        order.RegisterPhysicalShipment(false);
        Assert.Equal(OrderStatus.AwaitingProcessing, order.Status);
        Assert.Equal(OrderFulfillmentStatus.InProgress, order.FulfillmentStatus);
        Assert.Equal(OrderPaymentStatus.Paid, order.PaymentStatus);
        Assert.True(order.PhysicalDispatchStarted);
    }

    [Theory]
    [InlineData("ManualBankTransfer")]
    [InlineData("ManualCOD")]
    public void ReceiptOnlySupportsExplicitManualPaymentSources(string source)
    {
        var receipt = new PaymentReceipt(Guid.NewGuid(), Guid.NewGuid(), new string('a', 64), "RECEIPT-1", 10000, "VND", Now, Now, source);
        Assert.Equal(source, receipt.Source);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("manualcod")]
    [InlineData(" ManualCOD ")]
    [InlineData("Gateway")]
    public void ReceiptRejectsUnknownOrAmbiguousSource(string? source)
    {
        Assert.Throws<DomainException>(() => new PaymentReceipt(Guid.NewGuid(), Guid.NewGuid(), new string('a', 64),
            "RECEIPT-1", 10000, "VND", Now, Now, source!));
    }

    private static ShipmentItem Item(Guid orderId, int quantity = 1) =>
        new(orderId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), quantity);

    private static Shipment NewShipment(Guid? orderId = null, Guid? operationId = null,
        string hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", string carrier = "Carrier",
        string tracking = "TRACK-1", DateTime? now = null, ShipmentItem[]? items = null)
    {
        var id = orderId ?? Guid.NewGuid();
        return new Shipment(id, operationId ?? Guid.NewGuid(), hash, carrier, tracking, now ?? Now, items ?? [Item(id)]);
    }

    private static PhysicalStockReservation Reservation(int quantity) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), quantity);

    private static InventoryItem Balance(int onHand, int reserved)
    {
        var balance = new InventoryItem(Guid.NewGuid(), Guid.NewGuid());
        balance.Adjust(Guid.NewGuid(), onHand, "Opening", Guid.NewGuid());
        if (reserved > 0)
            balance.Reserve(reserved);
        return balance;
    }

    private static OrderItem OrderLine(ProductFulfillmentKind kind) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Product", "SKU", "", kind, 10000, 2);

    private static Order NewOrder(OrderPaymentMethod method = OrderPaymentMethod.Prepaid, OrderItem[]? items = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('a', 64), method,
            "Customer", "customer@example.com", "0123456789", "1 Street", 0, Now, 30,
            items ?? [OrderLine(ProductFulfillmentKind.Physical)]);
}
