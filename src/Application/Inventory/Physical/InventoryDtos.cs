using Boilerate.Domain.Inventory;

namespace Boilerate.Application.Inventory.Physical;

public sealed record InventoryDto(
    Guid Id,
    Guid VariantId,
    Guid LocationId,
    int OnHand,
    string Version,
    int Reserved = 0)
{
    public int Available => OnHand - Reserved;
}

public sealed record MovementDto(
    Guid Id,
    Guid InventoryItemId,
    Guid OperationId,
    int Delta,
    int QuantityAfter,
    string Reason,
    Guid ActorId,
    DateTime CreatedAt)
{
    public static MovementDto FromEntity(StockMovement m) =>
        new(m.Id, m.InventoryItemId, m.OperationId, m.Delta, m.QuantityAfter, m.Reason, m.ActorId, m.CreatedAt);
}
