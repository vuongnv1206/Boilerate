using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Contracts;
using Boilerate.Domain.Common.Exceptions;
namespace Boilerate.Domain.Ordering;

public enum HoldState
{
    Held = 1, Released = 2
}

public sealed class PhysicalStockReservation : CommerceEntity, IAggregateRoot
{
    public Guid OrderItemId { get; private set; }
    public Guid VariantId { get; private set; }
    public Guid InventoryItemId { get; private set; }
    public int Quantity { get; private set; }
    public HoldState State { get; private set; } = HoldState.Held;

    private PhysicalStockReservation()
    {
    }

    public PhysicalStockReservation(Guid orderItemId, Guid variantId, Guid inventoryItemId, int quantity)
    {
        if (orderItemId == Guid.Empty || variantId == Guid.Empty || inventoryItemId == Guid.Empty || quantity < 1)
            throw new DomainException("A reservation requires order item, SKU, balance IDs and a positive quantity.");
        OrderItemId = orderItemId;
        VariantId = variantId;
        InventoryItemId = inventoryItemId;
        Quantity = quantity;
    }

    public bool Release()
    {
        if (State == HoldState.Released)
            return false;
        State = HoldState.Released;
        Touch();
        return true;
    }

    public bool Reacquire()
    {
        if (State == HoldState.Held)
            return false;
        State = HoldState.Held;
        Touch();
        return true;
    }
}
