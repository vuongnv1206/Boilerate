using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Contracts;
using Boilerate.Domain.Common.Exceptions;
namespace Boilerate.Domain.Payments;

public sealed class Refund : CommerceEntity, IAggregateRoot
{
    public Guid ReceiptId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid OperationId { get; private set; }
    public string RequestHash { get; private set; } = null!;
    public string Reference { get; private set; } = null!;
    public string NormalizedReference { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;
    public DateTime RefundedAtUtc { get; private set; }
    public string Reason { get; private set; } = null!;
    public string Source { get; private set; } = null!;
    public Guid ActorId { get; private set; }

    private Refund()
    {
    }

    public Refund(Guid receiptId, Guid orderId, Guid operationId, string requestHash, string reference,
        decimal amount, string currency, DateTime refundedAtUtc, DateTime receiptAtUtc, DateTime now,
        string reason, string source, Guid actorId)
    {
        if (receiptId == Guid.Empty || orderId == Guid.Empty || operationId == Guid.Empty || actorId == Guid.Empty)
            throw new DomainException("Receipt, order, refund operation and actor IDs are required.");
        if (requestHash is null || requestHash.Length != 64 || requestHash.Any(c => !char.IsAsciiHexDigit(c)))
            throw new DomainException("RequestHash must be a SHA-256 hexadecimal value.");
        if (refundedAtUtc.Kind != DateTimeKind.Utc || receiptAtUtc.Kind != DateTimeKind.Utc || now.Kind != DateTimeKind.Utc)
            throw new DomainException("Refund time must be UTC, at or after receipt and no later than now.");
        // Compare at the shared database precision: SQL Server receipts may retain 100 ns ticks.
        var refundTime = UtcMicroseconds(refundedAtUtc);
        var receiptTime = UtcMicroseconds(receiptAtUtc);
        var currentTime = UtcMicroseconds(now);
        if (refundTime < receiptTime || refundTime > currentTime)
            throw new DomainException("Refund time must be UTC, at or after receipt and no later than now.");
        if (source is not "ManualBankTransfer" and not "ManualCash")
            throw new DomainException("Only verified manual bank transfer or cash refunds are supported.");
        CatalogRules.Money(amount, null);
        if (amount <= 0)
            throw new DomainException("A refund must be a positive whole VND amount.");
        var validReference = CatalogRules.Code(reference);
        var validCurrency = CatalogRules.Required(currency, 3, "Currency").ToUpperInvariant();
        if (validCurrency != "VND")
            throw new DomainException("Only VND refunds are supported.");
        var validReason = CatalogRules.Required(reason, 1000, "Reason");
        ReceiptId = receiptId;
        OrderId = orderId;
        OperationId = operationId;
        RequestHash = requestHash;
        Reference = validReference;
        NormalizedReference = validReference.ToUpperInvariant();
        Amount = amount;
        Currency = validCurrency;
        RefundedAtUtc = refundTime;
        Reason = validReason;
        Source = source;
        ActorId = actorId;
    }

    private static DateTime UtcMicroseconds(DateTime value) => new(value.Ticks - value.Ticks % 10, DateTimeKind.Utc);
}
