using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Contracts;
using Boilerate.Domain.Common.Exceptions;
namespace Boilerate.Domain.Payments;

public sealed class PaymentReceipt : CommerceEntity, IAggregateRoot
{
    public Guid OrderId { get; private set; }
    public Guid OperationId { get; private set; }
    public string RequestHash { get; private set; } = null!;
    public string Source { get; private set; } = "ManualBankTransfer";
    public string Reference { get; private set; } = null!;
    public string NormalizedReference { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;
    public DateTime ReceivedAtUtc { get; private set; }

    private PaymentReceipt()
    {
    }

    public PaymentReceipt(Guid orderId, Guid operationId, string requestHash, string reference, decimal amount,
        string currency, DateTime receivedAtUtc, DateTime now, string source = "ManualBankTransfer")
    {
        if (orderId == Guid.Empty || operationId == Guid.Empty)
            throw new DomainException("Order and payment operation IDs are required.");
        if (requestHash is null || requestHash.Length != 64 || requestHash.Any(c => !char.IsAsciiHexDigit(c)))
            throw new DomainException("RequestHash must be a SHA-256 hexadecimal value.");
        if (receivedAtUtc.Kind != DateTimeKind.Utc || now.Kind != DateTimeKind.Utc || receivedAtUtc > now)
            throw new DomainException("Payment receipt time must be UTC and cannot be in the future.");
        if (source is not "ManualBankTransfer" and not "ManualCOD")
            throw new DomainException("Only manual bank transfer or COD payment receipts are supported.");
        var validReference = CatalogRules.Code(reference);
        var validCurrency = CatalogRules.Required(currency, 3, "Currency").ToUpperInvariant();
        if (validCurrency != "VND")
            throw new DomainException("Only VND payment receipts are supported.");
        CatalogRules.Money(amount, null);
        OrderId = orderId;
        OperationId = operationId;
        RequestHash = requestHash;
        Source = source;
        Reference = validReference;
        NormalizedReference = validReference.ToUpperInvariant();
        Amount = amount;
        Currency = validCurrency;
        ReceivedAtUtc = receivedAtUtc;
    }
}
