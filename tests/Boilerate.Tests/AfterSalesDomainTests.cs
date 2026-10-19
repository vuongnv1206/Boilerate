using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Payments;
using Boilerate.Domain.Renewals;
using Boilerate.Domain.Shipping;
using Xunit;
namespace Boilerate.Tests;

public sealed class AfterSalesDomainTests
{
    private static readonly DateTime Now = new(2026, 10, 19, 8, 0, 0, DateTimeKind.Utc);
    private const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Theory]
    [InlineData("ManualBankTransfer")]
    [InlineData("ManualCash")]
    public void RefundKeepsVerifiedReceiptEvidenceAndNormalizesReference(string source)
    {
        var receipt = Guid.NewGuid();
        var order = Guid.NewGuid();
        var operation = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var refund = NewRefund(receipt, order, operation, reference: " rf-1 ", currency: " vnd ",
            reason: "  Customer refund  ", source: source, actor: actor);
        Assert.Equal(receipt, refund.ReceiptId);
        Assert.Equal(order, refund.OrderId);
        Assert.Equal(operation, refund.OperationId);
        Assert.Equal(Hash, refund.RequestHash);
        Assert.Equal("rf-1", refund.Reference);
        Assert.Equal("RF-1", refund.NormalizedReference);
        Assert.Equal(10000, refund.Amount);
        Assert.Equal("VND", refund.Currency);
        Assert.Equal("Customer refund", refund.Reason);
        Assert.Equal(source, refund.Source);
        Assert.Equal(actor, refund.ActorId);
        Assert.Equal(Now, refund.RefundedAtUtc);
    }

    [Fact]
    public void RefundRejectsMissingLinksMalformedHashAndInvalidEvidence()
    {
        Assert.Throws<DomainException>(() => NewRefund(receipt: Guid.Empty));
        Assert.Throws<DomainException>(() => NewRefund(order: Guid.Empty));
        Assert.Throws<DomainException>(() => NewRefund(operation: Guid.Empty));
        Assert.Throws<DomainException>(() => NewRefund(actor: Guid.Empty));
        Assert.Throws<DomainException>(() => NewRefund(hash: null!));
        Assert.Throws<DomainException>(() => NewRefund(hash: "bad"));
        Assert.Throws<DomainException>(() => NewRefund(hash: new string('g', 64)));
        Assert.Throws<DomainException>(() => NewRefund(reference: " "));
        Assert.Throws<DomainException>(() => NewRefund(reference: "Ref 1"));
        Assert.Throws<DomainException>(() => NewRefund(reference: "hoàn"));
        Assert.Throws<DomainException>(() => NewRefund(reference: new string('a', 101)));
        Assert.Throws<DomainException>(() => NewRefund(reason: " "));
        Assert.Throws<DomainException>(() => NewRefund(reason: new string('a', 1001)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.5)]
    [InlineData(10000000000000000)]
    public void RefundRequiresAPositiveWholeVndAmount(decimal amount)
    {
        Assert.Throws<DomainException>(() => NewRefund(amount: amount));
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("")]
    [InlineData(null)]
    public void RefundRejectsUnsupportedCurrency(string? currency)
    {
        Assert.Throws<DomainException>(() => NewRefund(currency: currency!));
    }

    [Theory]
    [InlineData("ManualCOD")]
    [InlineData("manualcash")]
    [InlineData(" ManualCash ")]
    [InlineData("Gateway")]
    [InlineData(null)]
    public void RefundDoesNotInventOrNormalizeItsSource(string? source)
    {
        Assert.Throws<DomainException>(() => NewRefund(source: source!));
    }

    [Fact]
    public void RefundChronologyUsesActualUtcTimesAndStoresMicroseconds()
    {
        Assert.Throws<DomainException>(() => NewRefund(refundedAt: Now.AddTicks(10)));
        Assert.Throws<DomainException>(() => NewRefund(refundedAt: Now.AddHours(-1).AddTicks(-1)));
        Assert.Throws<DomainException>(() => NewRefund(refundedAt: Unspecified(Now)));
        Assert.Throws<DomainException>(() => NewRefund(receiptAt: Unspecified(Now.AddHours(-1))));
        Assert.Throws<DomainException>(() => NewRefund(now: Unspecified(Now)));
        var refund = NewRefund(refundedAt: Now.AddTicks(-1));
        Assert.Equal(Now.AddTicks(-10), refund.RefundedAtUtc);
        Assert.Equal(DateTimeKind.Utc, refund.RefundedAtUtc.Kind);
        Assert.Equal(Now.AddHours(-1), NewRefund(refundedAt: Now.AddHours(-1)).RefundedAtUtc);
    }

    [Fact]
    public void RefundAndReceiptChronologyShareMicrosecondPrecisionAcrossProviders()
    {
        var receiptTime = Now.AddTicks(1234567);
        var commonTime = Now.AddTicks(1234560);
        Assert.Equal(commonTime, NewRefund(refundedAt: receiptTime, receiptAt: receiptTime,
            now: receiptTime).RefundedAtUtc);
        Assert.Equal(commonTime, NewRefund(refundedAt: commonTime, receiptAt: receiptTime,
            now: receiptTime).RefundedAtUtc);
        Assert.Equal(commonTime, NewRefund(refundedAt: commonTime.AddTicks(9), receiptAt: commonTime,
            now: commonTime).RefundedAtUtc);
        Assert.Throws<DomainException>(() => NewRefund(refundedAt: commonTime.AddTicks(-10),
            receiptAt: receiptTime, now: receiptTime));
        Assert.Throws<DomainException>(() => NewRefund(refundedAt: commonTime.AddTicks(10),
            receiptAt: receiptTime, now: receiptTime));
    }

    [Fact]
    public void ReturnAttachesSnapshotLinesAndCannotBeMutatedThroughItsInputCollection()
    {
        var order = Guid.NewGuid();
        var operation = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var first = ReturnLine(3, 2);
        var input = new List<PhysicalReturnItem> { first };
        var physicalReturn = new PhysicalReturn(order, operation, Hash, " r-1 ", "  Damaged  ", Now.AddTicks(123), true, actor, input);
        input.Clear();
        input.Add(ReturnLine());
        Assert.Same(first, Assert.Single(physicalReturn.Items));
        Assert.Equal(physicalReturn.Id, first.ReturnId);
        Assert.Equal(order, first.OrderId);
        Assert.Equal(order, physicalReturn.OrderId);
        Assert.Equal(operation, physicalReturn.OperationId);
        Assert.Equal(actor, physicalReturn.ActorId);
        Assert.Equal(Hash, physicalReturn.RequestHash);
        Assert.Equal("r-1", physicalReturn.Reference);
        Assert.Equal("R-1", physicalReturn.NormalizedReference);
        Assert.Equal("Damaged", physicalReturn.Reason);
        Assert.True(physicalReturn.CloseRemainingCod);
        Assert.Equal(Now.AddTicks(120), physicalReturn.ReceivedAtUtc);
        Assert.Equal(3, first.Quantity);
        Assert.Equal(2, first.RestockQuantity);
        var readOnly = Assert.IsAssignableFrom<IList<PhysicalReturnItem>>(physicalReturn.Items);
        Assert.True(readOnly.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => readOnly.Clear());
    }

    [Fact]
    public void ReturnValidatesTheWholeRootBeforeAttachingAnyLine()
    {
        var item = ReturnLine();
        Assert.Throws<DomainException>(() => NewReturn(order: Guid.Empty, items: [item]));
        Assert.Throws<DomainException>(() => NewReturn(operation: Guid.Empty, items: [item]));
        Assert.Throws<DomainException>(() => NewReturn(actor: Guid.Empty, items: [item]));
        Assert.Throws<DomainException>(() => NewReturn(hash: null!, items: [item]));
        Assert.Throws<DomainException>(() => NewReturn(hash: "bad", items: [item]));
        Assert.Throws<DomainException>(() => NewReturn(hash: new string('g', 64), items: [item]));
        Assert.Throws<DomainException>(() => NewReturn(now: Unspecified(Now), items: [item]));
        Assert.Throws<DomainException>(() => NewReturn(reference: " ", items: [item]));
        Assert.Throws<DomainException>(() => NewReturn(reference: "r 1", items: [item]));
        Assert.Throws<DomainException>(() => NewReturn(reference: new string('a', 101), items: [item]));
        Assert.Throws<DomainException>(() => NewReturn(reason: " ", items: [item]));
        Assert.Throws<DomainException>(() => NewReturn(reason: new string('a', 1001), items: [item]));
        Assert.Equal(Guid.Empty, item.ReturnId);
        Assert.Equal(Guid.Empty, item.OrderId);
        var physicalReturn = NewReturn(items: [item]);
        Assert.Throws<DomainException>(() => NewReturn(items: [item]));
        Assert.Equal(physicalReturn.Id, item.ReturnId);
    }

    [Fact]
    public void ReturnRequiresOneToFiftyDistinctShipmentLinesButCanContainTheSameOrderLineFromDifferentShipments()
    {
        Assert.Throws<DomainException>(() => new PhysicalReturn(Guid.NewGuid(), Guid.NewGuid(), Hash, "R", "Reason", Now, false, Guid.NewGuid(), null!));
        Assert.Throws<DomainException>(() => NewReturn(items: []));
        var first = ReturnLine();
        var duplicate = new PhysicalReturnItem(first.ShipmentId, first.OrderItemId, first.VariantId, 1, 0, null);
        Assert.Throws<DomainException>(() => NewReturn(items: [first, duplicate]));
        Assert.Throws<DomainException>(() => NewReturn(items: [first, null!]));
        Assert.Equal(Guid.Empty, first.ReturnId);
        var anotherShipment = new PhysicalReturnItem(Guid.NewGuid(), first.OrderItemId, first.VariantId, 1, 0, null);
        Assert.Equal(2, NewReturn(items: [first, anotherShipment]).Items.Count);
        var oversized = Enumerable.Range(0, 51).Select(_ => ReturnLine()).ToArray();
        Assert.Throws<DomainException>(() => NewReturn(items: oversized));
        Assert.All(oversized, line => Assert.Equal(Guid.Empty, line.ReturnId));
        Assert.Equal(50, NewReturn(items: oversized.Take(50).ToArray()).Items.Count);
    }

    [Fact]
    public void ReturnLineRequiresEveryLinkAndABoundedReceivedQuantity()
    {
        var id = Guid.NewGuid();
        Assert.Throws<DomainException>(() => new PhysicalReturnItem(Guid.Empty, id, id, 1, 0, null));
        Assert.Throws<DomainException>(() => new PhysicalReturnItem(id, Guid.Empty, id, 1, 0, null));
        Assert.Throws<DomainException>(() => new PhysicalReturnItem(id, id, Guid.Empty, 1, 0, null));
        Assert.Throws<DomainException>(() => ReturnLine(0));
        Assert.Throws<DomainException>(() => ReturnLine(1000));
        Assert.Throws<DomainException>(() => ReturnLine(1, -1));
        Assert.Throws<DomainException>(() => ReturnLine(1, 2));
        Assert.Equal(999, ReturnLine(999).Quantity);
    }

    [Fact]
    public void ReturnLineRequiresAMovementExactlyWhenRestocking()
    {
        var id = Guid.NewGuid();
        Assert.Throws<DomainException>(() => new PhysicalReturnItem(id, id, id, 1, 1, null));
        Assert.Throws<DomainException>(() => new PhysicalReturnItem(id, id, id, 1, 1, Guid.Empty));
        Assert.Throws<DomainException>(() => new PhysicalReturnItem(id, id, id, 1, 0, id));
        Assert.Throws<DomainException>(() => new PhysicalReturnItem(id, id, id, 1, 0, Guid.Empty));
        Assert.Null(ReturnLine().StockMovementId);
        Assert.NotNull(ReturnLine(2, 1).StockMovementId);
    }

    [Fact]
    public void ClosingAPaidOrderStopsNewFulfillmentAndPreservesPaidEvidence()
    {
        var order = NewOrder();
        order.RecordPayment(Now);
        order.CompleteFulfillment(false);
        var version = order.Version;
        order.CloseFulfillment();
        Assert.Equal(OrderStatus.Closed, order.Status);
        Assert.Equal(OrderFulfillmentStatus.Stopped, order.FulfillmentStatus);
        Assert.Equal(OrderPaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal(Now, order.PaidAtUtc);
        Assert.False(order.PhysicalDispatchStarted);
        Assert.NotEqual(version, order.Version);
        version = order.Version;
        order.CloseFulfillment();
        Assert.Equal(version, order.Version);
        Assert.Throws<DomainException>(() => order.CompleteFulfillment(false));
        Assert.Throws<DomainException>(() => order.CompleteFulfillment(true));
        Assert.Throws<DomainException>(order.MarkFulfillmentReview);
        Assert.Throws<DomainException>(() => order.RegisterPhysicalShipment(false));
        Assert.Throws<DomainException>(order.Cancel);
        Assert.False(order.Expire(Now.AddHours(1)));
        Assert.Equal(version, order.Version);
    }

    [Fact]
    public void ClosingAnAlreadyCompletedOrderKeepsItsOriginalItemsAndReceipt()
    {
        var order = NewOrder();
        order.RecordPayment(Now);
        order.RegisterPhysicalShipment(true);
        var item = Assert.Single(order.Items);
        order.CloseFulfillment();
        Assert.Equal(OrderStatus.Closed, order.Status);
        Assert.Equal(OrderFulfillmentStatus.Stopped, order.FulfillmentStatus);
        Assert.Same(item, Assert.Single(order.Items));
        Assert.True(order.PhysicalDispatchStarted);
        Assert.Equal(OrderPaymentStatus.Paid, order.PaymentStatus);
    }

    [Fact]
    public void ClosingRequiresPaymentOrAnActualCodDispatch()
    {
        var prepaid = NewOrder();
        var cod = NewOrder(OrderPaymentMethod.CashOnDelivery);
        var version = prepaid.Version;
        Assert.Throws<DomainException>(prepaid.CloseFulfillment);
        Assert.Equal(version, prepaid.Version);
        version = cod.Version;
        Assert.Throws<DomainException>(cod.CloseFulfillment);
        Assert.Equal(version, cod.Version);
        cod.RegisterPhysicalShipment(false);
        cod.CloseFulfillment();
        Assert.Equal(OrderStatus.Closed, cod.Status);
        Assert.Equal(OrderPaymentStatus.Unpaid, cod.PaymentStatus);
        Assert.Null(cod.PaidAtUtc);
        Assert.True(cod.PhysicalDispatchStarted);
    }

    [Fact]
    public void MoneyReceivedAfterCodClosureNeverReopensStoppedFulfillment()
    {
        var order = NewOrder(OrderPaymentMethod.CashOnDelivery);
        order.RegisterPhysicalShipment(false);
        order.CloseFulfillment();
        var version = order.Version;
        order.RecordPayment(Now.AddMinutes(1));
        Assert.Equal(OrderStatus.Closed, order.Status);
        Assert.Equal(OrderFulfillmentStatus.Stopped, order.FulfillmentStatus);
        Assert.Equal(OrderPaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal(Now.AddMinutes(1), order.PaidAtUtc);
        Assert.NotEqual(version, order.Version);
        Assert.Throws<DomainException>(() => order.RegisterPhysicalShipment(true));
        Assert.Throws<DomainException>(() => order.CompleteFulfillment(true));
    }

    [Fact]
    public void ClosingPartiallyConsumedHoldReturnsOnlyUnshippedRemainderWithoutChangingItsHistory()
    {
        var hold = Reservation(5);
        hold.Consume(2);
        var version = hold.Version;
        Assert.Equal(3, hold.CloseRemainder());
        Assert.Equal(HoldState.Closed, hold.State);
        Assert.Equal(5, hold.Quantity);
        Assert.Equal(2, hold.ConsumedQuantity);
        Assert.Equal(0, hold.Remaining);
        Assert.NotEqual(version, hold.Version);
        version = hold.Version;
        Assert.Equal(0, hold.CloseRemainder());
        Assert.Throws<DomainException>(() => hold.Release());
        Assert.Throws<DomainException>(() => hold.Reacquire());
        Assert.Throws<DomainException>(() => hold.Consume(1));
        Assert.Equal(version, hold.Version);
    }

    [Fact]
    public void EntirelyUnshippedClosedHoldCannotBeReacquiredThroughLegacyRelease()
    {
        var hold = Reservation(2);
        Assert.Equal(2, hold.CloseRemainder());
        Assert.Equal(0, hold.ConsumedQuantity);
        Assert.Equal(0, hold.Remaining);
        var version = hold.Version;
        Assert.Throws<DomainException>(() => hold.Release());
        Assert.Throws<DomainException>(() => hold.Reacquire());
        Assert.Equal(version, hold.Version);
    }

    [Fact]
    public void ReleasedAndFullyConsumedHoldsHaveNoNewRemainderToClose()
    {
        var released = Reservation(2);
        released.Release();
        var version = released.Version;
        Assert.Equal(0, released.CloseRemainder());
        Assert.Equal(HoldState.Released, released.State);
        Assert.Equal(version, released.Version);
        var consumed = Reservation(2);
        consumed.Consume(2);
        version = consumed.Version;
        Assert.Equal(0, consumed.CloseRemainder());
        Assert.Equal(HoldState.Consumed, consumed.State);
        Assert.Equal(version, consumed.Version);
    }

    [Fact]
    public void StoppingUnstartedRenewalReleasesItsClaimButDoesNotPresumeAResult()
    {
        var request = ReadyRenewal();
        request.Claim(Guid.NewGuid(), Now, 15);
        var version = request.Version;
        Assert.True(request.ReleaseUnstarted());
        Assert.Equal(RenewalStatus.Ready, request.Status);
        Assert.Null(request.ClaimedBy);
        Assert.Null(request.ClaimToken);
        Assert.Null(request.ClaimUntilUtc);
        Assert.False(request.TargetLocked);
        Assert.Empty(request.Attempts);
        Assert.NotEqual(version, request.Version);
        version = request.Version;
        Assert.False(request.ReleaseUnstarted());
        Assert.Equal(version, request.Version);
    }

    [Fact]
    public void UnstartedReleaseCannotUnlockAnAccountUsedByAnEarlierAttempt()
    {
        var request = ReadyRenewal();
        var actor = Guid.NewGuid();
        var token = request.Claim(actor, Now, 15);
        var first = request.Start(actor, token, Guid.NewGuid(), Now);
        request.Verify(first.Id, false, null, actor, Now.AddMinutes(1), "verified-no-effect");
        request.Claim(actor, Now.AddMinutes(2), 15);
        Assert.True(request.ReleaseUnstarted());
        Assert.True(request.TargetLocked);
        Assert.Equal(RenewalStatus.Ready, request.Status);
        Assert.Throws<DomainException>(() => request.SetTarget("replacement", Hash, "masked"));
        Assert.Equal(RenewalOutcome.NotApplied, first.Outcome);
    }

    [Fact]
    public void StoppingAnOrderCannotReleaseARenewalWithAnUnknownExternalOutcome()
    {
        var request = ReadyRenewal();
        var actor = Guid.NewGuid();
        var token = request.Claim(actor, Now, 15);
        var attempt = request.Start(actor, token, Guid.NewGuid(), Now);
        var version = request.Version;
        Assert.False(request.ReleaseUnstarted());
        Assert.False(request.ReleaseExpired(Now.AddYears(1)));
        Assert.Equal(version, request.Version);
        Assert.True(request.TargetLocked);
        Assert.Equal(RenewalStatus.PendingVerification, request.Status);
        Assert.Equal(RenewalOutcome.Pending, attempt.Outcome);
        Assert.Throws<DomainException>(() => request.Claim(Guid.NewGuid(), Now.AddYears(1), 15));
        request.Verify(attempt.Id, true, Now.AddDays(30), actor, Now.AddMinutes(1), "actual-result");
        version = request.Version;
        Assert.False(request.ReleaseUnstarted());
        Assert.Equal(version, request.Version);
        Assert.Equal(RenewalStatus.Completed, request.Status);
    }

    private static Refund NewRefund(Guid? receipt = null, Guid? order = null, Guid? operation = null,
        string hash = Hash, string reference = "RF-1", decimal amount = 10000, string currency = "VND",
        DateTime? refundedAt = null, DateTime? receiptAt = null, DateTime? now = null,
        string reason = "Customer refund", string source = "ManualBankTransfer", Guid? actor = null) =>
        new(receipt ?? Guid.NewGuid(), order ?? Guid.NewGuid(), operation ?? Guid.NewGuid(), hash, reference,
            amount, currency, refundedAt ?? Now, receiptAt ?? Now.AddHours(-1), now ?? Now, reason, source, actor ?? Guid.NewGuid());

    private static PhysicalReturn NewReturn(Guid? order = null, Guid? operation = null, string hash = Hash,
        string reference = "RETURN-1", string reason = "Damaged", DateTime? now = null,
        Guid? actor = null, PhysicalReturnItem[]? items = null) =>
        new(order ?? Guid.NewGuid(), operation ?? Guid.NewGuid(), hash, reference, reason, now ?? Now,
            false, actor ?? Guid.NewGuid(), items ?? [ReturnLine()]);

    private static PhysicalReturnItem ReturnLine(int quantity = 1, int restockQuantity = 0) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), quantity, restockQuantity,
            restockQuantity > 0 ? Guid.NewGuid() : null);

    private static PhysicalStockReservation Reservation(int quantity) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), quantity);

    private static Order NewOrder(OrderPaymentMethod method = OrderPaymentMethod.Prepaid) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Hash, method, "Customer", "customer@example.com",
            "0123456789", "1 Street", 0, Now, 30,
            [new OrderItem(Guid.NewGuid(), Guid.NewGuid(), "Product", "SKU", "", ProductFulfillmentKind.Physical, 10000, 2)]);

    private static RenewalRequest ReadyRenewal()
    {
        var request = new RenewalRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        request.SetTarget("protected-target", Hash, "masked");
        return request;
    }

    private static DateTime Unspecified(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
}
