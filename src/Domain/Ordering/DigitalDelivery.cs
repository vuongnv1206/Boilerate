using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Contracts;
using Boilerate.Domain.Common.Exceptions;
namespace Boilerate.Domain.Ordering;

public sealed class DigitalDelivery : CommerceEntity, IAggregateRoot
{
    public Guid OrderId { get; private set; }
    public Guid OrderItemId { get; private set; }
    public Guid VariantId { get; private set; }
    public Guid StockItemId { get; private set; }
    public int Sequence { get; private set; }
    public ProductFulfillmentKind FulfillmentKind { get; private set; }
    public string ProtectedContent { get; private set; } = null!;

    private DigitalDelivery()
    {
    }

    public DigitalDelivery(Guid id, Guid orderId, Guid orderItemId, Guid variantId, Guid stockItemId,
        int sequence, ProductFulfillmentKind kind, string protectedContent)
    {
        if (id == Guid.Empty || orderId == Guid.Empty || orderItemId == Guid.Empty || variantId == Guid.Empty || stockItemId == Guid.Empty)
            throw new DomainException("Delivery, order, item, SKU and stock IDs are required.");
        if (sequence is < 1 or > 999)
            throw new DomainException("A delivery sequence must be from 1 to 999.");
        if (kind is not ProductFulfillmentKind.DigitalUnique and not ProductFulfillmentKind.DigitalShared)
            throw new DomainException("Only Unique or Shared content can be delivered automatically.");
        if (string.IsNullOrWhiteSpace(protectedContent) || protectedContent.Length > 16000)
            throw new DomainException("Protected delivery content is required and must not exceed 16000 characters.");
        Id = id;
        OrderId = orderId;
        OrderItemId = orderItemId;
        VariantId = variantId;
        StockItemId = stockItemId;
        Sequence = sequence;
        FulfillmentKind = kind;
        ProtectedContent = protectedContent;
    }
}
