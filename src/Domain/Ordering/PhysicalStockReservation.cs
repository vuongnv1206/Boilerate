using System.ComponentModel.DataAnnotations.Schema;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Contracts;
using Boilerate.Domain.Common.Exceptions;
namespace Boilerate.Domain.Ordering;

public enum HoldState
{
    Held = 1, Released = 2, Consumed = 3, Closed = 4
}

public sealed class PhysicalStockReservation : CommerceEntity, IAggregateRoot
{
    public Guid OrderItemId { get; private set; }
    public Guid VariantId { get; private set; }
    public Guid InventoryItemId { get; private set; }
    public int Quantity { get; private set; }
    public int ConsumedQuantity { get; private set; }
    [NotMapped]
    public int Remaining => State == HoldState.Closed ? 0 : Quantity - ConsumedQuantity;
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
        if (State == HoldState.Closed)
            throw new DomainException("A closed reservation cannot be released or reopened.");
        if (ConsumedQuantity > 0)
            throw new DomainException("Dispatched stock cannot be released through reservation cancellation.");
        if (State == HoldState.Released)
            return false;
        State = HoldState.Released;
        Touch();
        return true;
    }

    public bool Reacquire()
    {
        if (State == HoldState.Closed)
            throw new DomainException("A closed reservation cannot be reacquired.");
        if (ConsumedQuantity > 0)
            throw new DomainException("Dispatched stock cannot be reacquired as a new reservation.");
        if (State == HoldState.Held)
            return false;
        State = HoldState.Held;
        Touch();
        return true;
    }

    public int CloseRemainder()
    {
        if (State != HoldState.Held || Remaining == 0)
            return 0;
        var remaining = Remaining;
        State = HoldState.Closed;
        Touch();
        return remaining;
    }

    public void Consume(int quantity)
    {
        if (State != HoldState.Held || quantity < 1 || quantity > Remaining)
            throw new DomainException("Stock consumption must be within the remaining held reservation.");
        ConsumedQuantity += quantity;
        if (Remaining == 0)
            State = HoldState.Consumed;
        Touch();
    }
}
