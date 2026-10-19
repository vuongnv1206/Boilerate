using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Contracts;
using Boilerate.Domain.Common.Exceptions;
namespace Boilerate.Domain.Shipping;

public sealed class PhysicalReturn : CommerceEntity, IAggregateRoot
{
    private readonly List<PhysicalReturnItem> _items = [];
    public Guid OrderId { get; private set; }
    public Guid OperationId { get; private set; }
    public string RequestHash { get; private set; } = null!;
    public string Reference { get; private set; } = null!;
    public string NormalizedReference { get; private set; } = null!;
    public string Reason { get; private set; } = null!;
    public DateTime ReceivedAtUtc { get; private set; }
    public bool CloseRemainingCod { get; private set; }
    public Guid ActorId { get; private set; }
    public IReadOnlyCollection<PhysicalReturnItem> Items => _items.AsReadOnly();

    private PhysicalReturn()
    {
    }

    public PhysicalReturn(Guid orderId, Guid operationId, string hash, string reference, string reason,
        DateTime now, bool closeRemainingCod, Guid actorId, IEnumerable<PhysicalReturnItem> items)
    {
        if (orderId == Guid.Empty || operationId == Guid.Empty || actorId == Guid.Empty)
            throw new DomainException("Order, return operation and actor IDs are required.");
        if (hash is null || hash.Length != 64 || hash.Any(c => !char.IsAsciiHexDigit(c)))
            throw new DomainException("RequestHash must be a SHA-256 hexadecimal value.");
        if (now.Kind != DateTimeKind.Utc)
            throw new DomainException("Physical return time must be UTC.");
        if (items is null)
            throw new DomainException("Physical return items are required.");
        var snapshot = items.Take(51).ToList();
        if (snapshot.Count is < 1 or > 50 || snapshot.Any(i => i is null || i.ReturnId != Guid.Empty || i.OrderId != Guid.Empty) ||
            snapshot.Select(i => (i.ShipmentId, i.OrderItemId)).Distinct().Count() != snapshot.Count)
            throw new DomainException("A return requires 1 to 50 distinct, unattached shipment lines.");
        var validReference = CatalogRules.Code(reference);
        var validReason = CatalogRules.Required(reason, 1000, "Reason");
        OrderId = orderId;
        OperationId = operationId;
        RequestHash = hash;
        Reference = validReference;
        NormalizedReference = validReference.ToUpperInvariant();
        Reason = validReason;
        ReceivedAtUtc = new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc);
        CloseRemainingCod = closeRemainingCod;
        ActorId = actorId;
        foreach (var item in snapshot)
        {
            item.Attach(Id, orderId);
            _items.Add(item);
        }
    }
}

public sealed class PhysicalReturnItem
{
    public Guid ReturnId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid ShipmentId { get; private set; }
    public Guid OrderItemId { get; private set; }
    public Guid VariantId { get; private set; }
    public int Quantity { get; private set; }
    public int RestockQuantity { get; private set; }
    public Guid? StockMovementId { get; private set; }

    private PhysicalReturnItem()
    {
    }

    public PhysicalReturnItem(Guid shipmentId, Guid orderItemId, Guid variantId, int quantity,
        int restockQuantity, Guid? stockMovementId)
    {
        if (shipmentId == Guid.Empty || orderItemId == Guid.Empty || variantId == Guid.Empty)
            throw new DomainException("Shipment, order item and SKU IDs are required for a return line.");
        if (quantity is < 1 or > 999 || restockQuantity < 0 || restockQuantity > quantity)
            throw new DomainException("Return quantity must be 1 to 999, with restock quantity from zero to quantity returned.");
        if ((restockQuantity > 0 && (!stockMovementId.HasValue || stockMovementId == Guid.Empty)) ||
            (restockQuantity == 0 && stockMovementId.HasValue))
            throw new DomainException("A stock movement is required exactly when returned stock is restocked.");
        ShipmentId = shipmentId;
        OrderItemId = orderItemId;
        VariantId = variantId;
        Quantity = quantity;
        RestockQuantity = restockQuantity;
        StockMovementId = stockMovementId;
    }

    internal void Attach(Guid returnId, Guid orderId)
    {
        if (returnId == Guid.Empty || orderId == Guid.Empty || ReturnId != Guid.Empty || OrderId != Guid.Empty)
            throw new DomainException("A physical return line may only be attached to one return.");
        ReturnId = returnId;
        OrderId = orderId;
    }
}
