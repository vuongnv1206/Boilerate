using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Contracts;
using Boilerate.Domain.Common.Exceptions;
namespace Boilerate.Domain.Shipping;

public enum ShipmentStatus
{
    Dispatched = 1, Delivered = 2
}

public sealed class Shipment : CommerceEntity, IAggregateRoot
{
    private readonly List<ShipmentItem> _items = [];
    public Guid OrderId { get; private set; }
    public Guid OperationId { get; private set; }
    public string RequestHash { get; private set; } = null!;
    public string Carrier { get; private set; } = null!;
    public string TrackingNumber { get; private set; } = null!;
    public ShipmentStatus Status { get; private set; } = ShipmentStatus.Dispatched;
    public DateTime DispatchedAtUtc { get; private set; }
    public DateTime? DeliveredAtUtc { get; private set; }
    public Guid? DeliveryOperationId { get; private set; }
    public IReadOnlyCollection<ShipmentItem> Items => _items;

    private Shipment()
    {
    }

    public Shipment(Guid orderId, Guid operationId, string requestHash, string carrier, string trackingNumber,
        DateTime now, IEnumerable<ShipmentItem> items)
    {
        if (orderId == Guid.Empty || operationId == Guid.Empty)
            throw new DomainException("Order and shipment operation IDs are required.");
        if (requestHash is null || requestHash.Length != 64 || requestHash.Any(c => !char.IsAsciiHexDigit(c)))
            throw new DomainException("RequestHash must be a SHA-256 hexadecimal value.");
        if (now.Kind != DateTimeKind.Utc)
            throw new DomainException("Shipment dispatch time must be UTC.");
        if (items is null)
            throw new DomainException("Shipment items are required.");
        var snapshot = items.Take(51).ToList();
        if (snapshot.Count is < 1 or > 50 ||
            snapshot.Any(i => i is null || i.ShipmentId != Guid.Empty || i.OrderId != orderId) ||
            snapshot.Select(i => i.OrderItemId).Distinct().Count() != snapshot.Count)
            throw new DomainException("A shipment requires 1 to 50 distinct, unattached items belonging to its order.");
        var validCarrier = CatalogRules.Required(carrier, 100, "Carrier");
        var validTracking = CatalogRules.Required(trackingNumber, 100, "TrackingNumber");
        OrderId = orderId;
        OperationId = operationId;
        RequestHash = requestHash;
        Carrier = validCarrier;
        TrackingNumber = validTracking;
        DispatchedAtUtc = UtcMicroseconds(now);
        foreach (var item in snapshot)
        {
            item.Attach(Id);
            _items.Add(item);
        }
    }

    public bool MarkDelivered(Guid operationId, DateTime now)
    {
        if (operationId == Guid.Empty || now.Kind != DateTimeKind.Utc || now < DispatchedAtUtc)
            throw new DomainException("Delivery requires an operation ID and UTC time at or after dispatch.");
        if (Status == ShipmentStatus.Delivered)
        {
            if (DeliveryOperationId == operationId)
                return false;
            throw new DomainException("This shipment has already been delivered by another operation.");
        }
        Status = ShipmentStatus.Delivered;
        DeliveryOperationId = operationId;
        DeliveredAtUtc = UtcMicroseconds(now);
        Touch();
        return true;
    }

    // PostgreSQL stores microseconds; normalize before returning the initial response as well.
    private static DateTime UtcMicroseconds(DateTime value) => new(value.Ticks - value.Ticks % 10, DateTimeKind.Utc);
}

public sealed class ShipmentItem
{
    public Guid ShipmentId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid OrderItemId { get; private set; }
    public Guid VariantId { get; private set; }
    public Guid? StockMovementId { get; private set; }
    public ProductFulfillmentKind FulfillmentKind { get; private set; } = ProductFulfillmentKind.Physical;
    public int Quantity { get; private set; }

    private ShipmentItem()
    {
    }

    public ShipmentItem(Guid orderId, Guid orderItemId, Guid variantId, Guid? stockMovementId, int quantity)
    {
        if (orderId == Guid.Empty || orderItemId == Guid.Empty || variantId == Guid.Empty || stockMovementId == Guid.Empty || quantity is < 1 or > 999)
            throw new DomainException("Shipment items require order, item, SKU and movement IDs and quantity from 1 to 999.");
        OrderId = orderId;
        OrderItemId = orderItemId;
        VariantId = variantId;
        StockMovementId = stockMovementId;
        Quantity = quantity;
    }

    internal void Attach(Guid shipmentId)
    {
        if (shipmentId == Guid.Empty || ShipmentId != Guid.Empty)
            throw new DomainException("A shipment item may only be attached once.");
        ShipmentId = shipmentId;
    }
}
