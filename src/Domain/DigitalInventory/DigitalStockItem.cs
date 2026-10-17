using System.Text;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Contracts;
using Boilerate.Domain.Common.Exceptions;
namespace Boilerate.Domain.DigitalInventory;

public enum DigitalStockState
{
    Available = 1, Disabled = 2
}
public sealed class DigitalStockItem : CommerceEntity, IAggregateRoot
{
    public Guid ProductId { get; private set; }
    public Guid VariantId { get; private set; }
    public ProductFulfillmentKind FulfillmentKind { get; private set; }
    public string Reference { get; private set; } = null!;
    public string ProtectedContent { get; private set; } = null!;
    public DigitalStockState State { get; private set; } = DigitalStockState.Available;

    private DigitalStockItem()
    {
    }

    public DigitalStockItem(Guid id, Guid productId, Guid variantId, ProductFulfillmentKind fulfillmentKind, string reference, string protectedContent)
    {
        if (id == Guid.Empty || productId == Guid.Empty || variantId == Guid.Empty)
            throw new DomainException("Digital stock, product and variant IDs are required.");
        if (fulfillmentKind is not ProductFulfillmentKind.DigitalUnique and not ProductFulfillmentKind.DigitalShared)
            throw new DomainException("Only Unique or Shared products can have digital stock.");
        var normalizedReference = CatalogRules.Code(reference).ToUpperInvariant();
        if (fulfillmentKind == ProductFulfillmentKind.DigitalShared && normalizedReference != "SHARED")
            throw new DomainException("Shared content must use the SHARED reference.");
        Id = id;
        ProductId = productId;
        VariantId = variantId;
        FulfillmentKind = fulfillmentKind;
        Reference = normalizedReference;
        ProtectedContent = ValidateProtectedContent(protectedContent);
    }

    public static string ValidateContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content) || Encoding.UTF8.GetByteCount(content) > 8000)
            throw new DomainException("Digital content is required and must not exceed 8000 UTF-8 bytes.");
        return content;
    }

    public void ReplaceSharedContent(string protectedContent)
    {
        if (FulfillmentKind != ProductFulfillmentKind.DigitalShared)
            throw new DomainException("Unique digital content cannot be replaced.");
        ProtectedContent = ValidateProtectedContent(protectedContent);
        Touch();
    }

    public void SetAvailability(bool available)
    {
        State = available ? DigitalStockState.Available : DigitalStockState.Disabled;
        Touch();
    }

    private static string ValidateProtectedContent(string protectedContent)
    {
        if (string.IsNullOrWhiteSpace(protectedContent) || protectedContent.Length > 16000)
            throw new DomainException("Protected digital content is required and must not exceed 16000 characters.");
        return protectedContent;
    }
}
