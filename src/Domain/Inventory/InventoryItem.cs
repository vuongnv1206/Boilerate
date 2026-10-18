using System.ComponentModel.DataAnnotations.Schema;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Contracts;
using Boilerate.Domain.Common.Exceptions;
namespace Boilerate.Domain.Inventory;

public sealed class InventoryLocation : CommerceEntity, IAggregateRoot
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public bool IsActive { get; private set; } = true;
    private InventoryLocation()
    {
    }
    public InventoryLocation(string code, string name)
    {
        Code = CatalogRules.Code(code).ToUpperInvariant();
        Name = CatalogRules.Required(name, 256, "Name");
    }
}
public sealed class InventoryItem : CommerceEntity, IAggregateRoot
{
    public Guid VariantId
    {
        get; private set;
    }
    public Guid LocationId
    {
        get; private set;
    }
    public int OnHand
    {
        get; private set;
    }
    public int Reserved { get; private set; }
    [NotMapped]
    public int Available => OnHand - Reserved;
    private InventoryItem()
    {
    }
    public InventoryItem(Guid variantId, Guid locationId)
    {
        VariantId = variantId;
        LocationId = locationId;
    }
    public StockMovement Adjust(Guid operationId, int delta, string reason, Guid actor)
    {
        if (operationId == Guid.Empty || delta == 0)
            throw new DomainException("OperationId and a nonzero delta are required.");
        long next = (long)OnHand + delta;
        if (next < Reserved || next > int.MaxValue)
            throw new DomainException("Adjustment would exceed inventory limits.");
        reason = CatalogRules.Required(reason, 1000, "Reason");
        OnHand = (int)next;
        Touch();
        return new StockMovement(Id, operationId, delta, OnHand, reason, actor);
    }
    public void Reserve(int quantity)
    {
        if (quantity < 1 || quantity > Available)
            throw new DomainException("Reservation exceeds available inventory.");
        Reserved += quantity;
        Touch();
    }
    public void Release(int quantity)
    {
        if (quantity < 1 || quantity > Reserved)
            throw new DomainException("Release exceeds reserved inventory.");
        Reserved -= quantity;
        Touch();
    }
    public StockMovement ConsumeReserved(Guid operationId, int quantity, string reason, Guid actor)
    {
        if (operationId == Guid.Empty || actor == Guid.Empty || quantity < 1 || quantity > Reserved || quantity > OnHand)
            throw new DomainException("Reserved stock consumption requires operation and actor IDs and a quantity within held inventory.");
        reason = CatalogRules.Required(reason, 1000, "Reason");
        OnHand -= quantity;
        Reserved -= quantity;
        Touch();
        return new StockMovement(Id, operationId, -quantity, OnHand, reason, actor);
    }
}
public sealed class StockMovement : BaseEntity
{
    public Guid InventoryItemId
    {
        get; private set;
    }
    public Guid OperationId
    {
        get; private set;
    }
    public int Delta
    {
        get; private set;
    }
    public int QuantityAfter
    {
        get; private set;
    }
    public string Reason { get; private set; } = null!;
    public Guid ActorId
    {
        get; private set;
    }
    public DateTime CreatedAt
    {
        get; private set;
    }
    private StockMovement()
    {
    }
    internal StockMovement(Guid itemId, Guid operationId, int delta, int after, string reason, Guid actor)
    {
        InventoryItemId = itemId;
        OperationId = operationId;
        Delta = delta;
        QuantityAfter = after;
        Reason = reason;
        ActorId = actor;
        var now = DateTime.UtcNow;
        CreatedAt = new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc);
    }
}
