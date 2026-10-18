using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.DigitalInventory;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Payments;
using Xunit;
namespace Boilerate.Tests;

public sealed class PaymentDomainTests
{
    private static readonly DateTime Now = new(2026, 10, 18, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ReceiptKeepsEvidenceAndNormalizesReferenceForDuplicateDetection()
    {
        var orderId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var received = Now.AddMinutes(-2);
        var receipt = new PaymentReceipt(orderId, operationId, new string('a', 64), "  bank-ref.01  ", 125000,
            " vnd ", received, Now);
        Assert.NotEqual(Guid.Empty, receipt.Id);
        Assert.Equal(orderId, receipt.OrderId);
        Assert.Equal(operationId, receipt.OperationId);
        Assert.Equal(new string('a', 64), receipt.RequestHash);
        Assert.Equal("ManualBankTransfer", receipt.Source);
        Assert.Equal("bank-ref.01", receipt.Reference);
        Assert.Equal("BANK-REF.01", receipt.NormalizedReference);
        Assert.Equal(125000m, receipt.Amount);
        Assert.Equal("VND", receipt.Currency);
        Assert.Equal(received, receipt.ReceivedAtUtc);
    }

    [Fact]
    public void ReceiptRejectsMissingIdentityMalformedHashAndInvalidBankReference()
    {
        Assert.Throws<DomainException>(() => Receipt(orderId: Guid.Empty));
        Assert.Throws<DomainException>(() => Receipt(operationId: Guid.Empty));
        Assert.Throws<DomainException>(() => Receipt(hash: "invalid"));
        Assert.Throws<DomainException>(() => Receipt(hash: new string('g', 64)));
        Assert.Throws<DomainException>(() => Receipt(reference: " "));
        Assert.Throws<DomainException>(() => Receipt(reference: new string('a', 101)));
        Assert.Throws<DomainException>(() => Receipt(reference: "invalid reference"));
        Assert.Throws<DomainException>(() => Receipt(reference: "giao-dịch"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0.5)]
    [InlineData(10000000000000000)]
    public void ReceiptRejectsAmountsOutsideWholeNonnegativeVnd(decimal amount)
    {
        Assert.Throws<DomainException>(() => Receipt(amount: amount));
    }

    [Fact]
    public void ReceiptRequiresVndAndUtcEvidenceNotInTheFuture()
    {
        Assert.Throws<DomainException>(() => Receipt(currency: "USD"));
        Assert.Throws<DomainException>(() => Receipt(received: Now.AddTicks(1)));
        Assert.Throws<DomainException>(() => Receipt(received: DateTime.SpecifyKind(Now, DateTimeKind.Unspecified)));
        Assert.Throws<DomainException>(() => new PaymentReceipt(Guid.NewGuid(), Guid.NewGuid(), new string('a', 64),
            "BANK-1", 1, "VND", Now, DateTime.SpecifyKind(Now, DateTimeKind.Local)));
        Assert.Equal(0m, Receipt(amount: 0).Amount);
        Assert.Equal(9999999999999999m, Receipt(amount: 9999999999999999m).Amount);
    }

    [Fact]
    public void PaymentIsIndependentFromFulfillmentAndPreventsCancellationOrExpiry()
    {
        var order = NewOrder();
        Assert.Equal(OrderPaymentStatus.Unpaid, order.PaymentStatus);
        Assert.Equal(OrderFulfillmentStatus.Pending, order.FulfillmentStatus);
        Assert.Null(order.PaidAtUtc);
        var version = order.Version;
        order.RecordPayment(Now);
        Assert.Equal(OrderPaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal(Now, order.PaidAtUtc);
        Assert.Equal(OrderStatus.AwaitingProcessing, order.Status);
        Assert.Equal(OrderFulfillmentStatus.NeedsReview, order.FulfillmentStatus);
        Assert.NotEqual(version, order.Version);
        version = order.Version;
        Assert.Throws<DomainException>(order.Cancel);
        Assert.False(order.Expire(Now.AddHours(1)));
        Assert.Throws<DomainException>(() => order.RecordPayment(Now));
        Assert.Equal(version, order.Version);
        Assert.Equal(OrderPaymentStatus.Paid, order.PaymentStatus);
    }

    [Fact]
    public void InvalidPaymentTimeAndUnpaidFulfillmentLeaveOrderUnchanged()
    {
        var order = NewOrder();
        var version = order.Version;
        Assert.Throws<DomainException>(() => order.RecordPayment(DateTime.SpecifyKind(Now, DateTimeKind.Unspecified)));
        Assert.Throws<DomainException>(() => order.CompleteFulfillment(true));
        Assert.Throws<DomainException>(order.MarkFulfillmentReview);
        Assert.Equal(version, order.Version);
        Assert.Equal(OrderPaymentStatus.Unpaid, order.PaymentStatus);
        Assert.Null(order.PaidAtUtc);
    }

    [Fact]
    public void PaymentAfterExpiryCanResumeOnlyThroughTheFulfillmentWorkflow()
    {
        var order = NewOrder();
        Assert.True(order.Expire(Now.AddMinutes(30)));
        order.RecordPayment(Now.AddMinutes(35));
        Assert.Equal(OrderStatus.AwaitingProcessing, order.Status);
        Assert.Equal(OrderPaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal(OrderFulfillmentStatus.NeedsReview, order.FulfillmentStatus);
        order.CompleteFulfillment(true);
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal(OrderFulfillmentStatus.Completed, order.FulfillmentStatus);
    }

    [Fact]
    public void MoneyReceivedForCancelledOrderIsRetainedForReviewWithoutRevivingOrDelivering()
    {
        var order = NewOrder();
        order.Cancel();
        order.RecordPayment(Now);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(OrderPaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal(OrderFulfillmentStatus.NeedsReview, order.FulfillmentStatus);
        var version = order.Version;
        Assert.Throws<DomainException>(() => order.CompleteFulfillment(true));
        Assert.Throws<DomainException>(() => order.CompleteFulfillment(false));
        Assert.Throws<DomainException>(order.Cancel);
        Assert.Equal(version, order.Version);
        order.MarkFulfillmentReview();
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void PhysicalOrManualWorkRemainsInProgressAfterReceivingMoney()
    {
        var order = NewOrder(ProductFulfillmentKind.ManualExtension);
        order.RecordPayment(Now);
        order.CompleteFulfillment(false);
        Assert.Equal(OrderPaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal(OrderStatus.AwaitingProcessing, order.Status);
        Assert.Equal(OrderFulfillmentStatus.InProgress, order.FulfillmentStatus);
        order.MarkFulfillmentReview();
        Assert.Equal(OrderFulfillmentStatus.NeedsReview, order.FulfillmentStatus);
        Assert.Equal(OrderPaymentStatus.Paid, order.PaymentStatus);
    }

    [Fact]
    public void ReleasedPhysicalReservationCanBeReacquiredOnceWithoutChangingItsIdentity()
    {
        var hold = new PhysicalStockReservation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2);
        var id = hold.Id;
        var version = hold.Version;
        Assert.False(hold.Reacquire());
        Assert.Equal(version, hold.Version);
        Assert.True(hold.Release());
        version = hold.Version;
        Assert.True(hold.Reacquire());
        Assert.Equal(id, hold.Id);
        Assert.Equal(2, hold.Quantity);
        Assert.Equal(HoldState.Held, hold.State);
        Assert.NotEqual(version, hold.Version);
        version = hold.Version;
        Assert.False(hold.Reacquire());
        Assert.Equal(version, hold.Version);
    }

    [Fact]
    public void UniqueDeliveryPermanentlyKeepsOwnershipAndCannotBeReleasedOrSoldAgain()
    {
        var stock = Stock();
        var owner = Guid.NewGuid();
        stock.Reserve(owner);
        var version = stock.Version;
        Assert.Throws<DomainException>(() => stock.Deliver(Guid.NewGuid()));
        Assert.Equal(version, stock.Version);
        stock.Deliver(owner);
        Assert.Equal(DigitalStockState.Delivered, stock.State);
        Assert.Equal(owner, stock.ReservedForOrderItemId);
        Assert.Equal("protected-stock", stock.ProtectedContent);
        Assert.NotEqual(version, stock.Version);
        version = stock.Version;
        Assert.Throws<DomainException>(() => stock.Release(owner));
        Assert.Throws<DomainException>(() => stock.Release(Guid.NewGuid()));
        Assert.Throws<DomainException>(() => stock.Reserve(Guid.NewGuid()));
        Assert.Throws<DomainException>(() => stock.Deliver(owner));
        Assert.Throws<DomainException>(() => stock.SetAvailability(true));
        Assert.Throws<DomainException>(() => stock.SetAvailability(false));
        Assert.Equal(version, stock.Version);
        Assert.Equal(owner, stock.ReservedForOrderItemId);
    }

    [Fact]
    public void DeliveryRequiresReservedUniqueStockAndTheOwningItem()
    {
        var stock = Stock();
        Assert.Throws<DomainException>(() => stock.Deliver(Guid.NewGuid()));
        stock.SetAvailability(false);
        Assert.Throws<DomainException>(() => stock.Deliver(Guid.NewGuid()));
        stock.SetAvailability(true);
        stock.Reserve(Guid.NewGuid());
        Assert.Throws<DomainException>(() => stock.Deliver(Guid.Empty));
        var shared = Stock(ProductFulfillmentKind.DigitalShared);
        Assert.Throws<DomainException>(() => shared.Deliver(Guid.NewGuid()));
    }

    [Fact]
    public void DeliverySnapshotRetainsExplicitIdentityAndCiphertext()
    {
        var id = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var stockId = Guid.NewGuid();
        var snapshot = new DigitalDelivery(id, orderId, itemId, variantId, stockId, 2,
            ProductFulfillmentKind.DigitalUnique, "protected-delivery");
        Assert.Equal(id, snapshot.Id);
        Assert.Equal(orderId, snapshot.OrderId);
        Assert.Equal(itemId, snapshot.OrderItemId);
        Assert.Equal(variantId, snapshot.VariantId);
        Assert.Equal(stockId, snapshot.StockItemId);
        Assert.Equal(2, snapshot.Sequence);
        Assert.Equal(ProductFulfillmentKind.DigitalUnique, snapshot.FulfillmentKind);
        Assert.Equal("protected-delivery", snapshot.ProtectedContent);
    }

    [Fact]
    public void DeliveryRejectsMissingLinksUnsupportedModesAndInvalidSequenceOrCiphertext()
    {
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        for (var index = 0; index < ids.Length; index++)
        {
            var invalid = ids.ToArray();
            invalid[index] = Guid.Empty;
            Assert.Throws<DomainException>(() => new DigitalDelivery(invalid[0], invalid[1], invalid[2], invalid[3], invalid[4],
                1, ProductFulfillmentKind.DigitalUnique, "protected-delivery"));
        }
        Assert.Throws<DomainException>(() => Delivery(sequence: 0));
        Assert.Throws<DomainException>(() => Delivery(sequence: 1000));
        Assert.Throws<DomainException>(() => Delivery(kind: ProductFulfillmentKind.Physical));
        Assert.Throws<DomainException>(() => Delivery(kind: ProductFulfillmentKind.ManualExtension));
        Assert.Throws<DomainException>(() => Delivery(kind: (ProductFulfillmentKind)99));
        Assert.Throws<DomainException>(() => Delivery(content: null!));
        Assert.Throws<DomainException>(() => Delivery(content: "  "));
        Assert.Throws<DomainException>(() => Delivery(content: new string('x', 16001)));
        Assert.Equal(16000, Delivery(sequence: 999, kind: ProductFulfillmentKind.DigitalShared, content: new string('x', 16000)).ProtectedContent.Length);
    }

    private static PaymentReceipt Receipt(Guid? orderId = null, Guid? operationId = null, string hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        string reference = "BANK-1", decimal amount = 10000, string currency = "VND", DateTime? received = null) =>
        new(orderId ?? Guid.NewGuid(), operationId ?? Guid.NewGuid(), hash, reference, amount, currency, received ?? Now, Now);

    private static Order NewOrder(ProductFulfillmentKind kind = ProductFulfillmentKind.DigitalUnique) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('a', 64), OrderPaymentMethod.Prepaid,
            "Customer", "customer@example.com", "0123456789", null, 0, Now, 30,
            [new OrderItem(Guid.NewGuid(), Guid.NewGuid(), "Product", "SKU", "", kind, 10000, 1)]);

    private static DigitalStockItem Stock(ProductFulfillmentKind kind = ProductFulfillmentKind.DigitalUnique) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), kind, kind == ProductFulfillmentKind.DigitalShared ? "SHARED" : "CODE-1", "protected-stock");

    private static DigitalDelivery Delivery(int sequence = 1, ProductFulfillmentKind kind = ProductFulfillmentKind.DigitalUnique,
        string content = "protected-delivery") =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), sequence, kind, content);
}
