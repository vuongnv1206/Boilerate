using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Inventory.Physical;

public sealed record AdjustInventoryRequest(
    Guid InventoryItemId,
    string Version,
    Guid OperationId,
    int Delta,
    string Reason) : IRequest<MovementDto>;

public sealed class AdjustInventoryRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor) : IRequestHandler<AdjustInventoryRequest, MovementDto>
{
    public async Task<MovementDto> Handle(AdjustInventoryRequest request, CancellationToken ct)
    {
        async Task<StockMovement?> ExistingAsync() =>
            await db.StockMovements.AsNoTracking().SingleOrDefaultAsync(m => m.OperationId == request.OperationId, ct);

        MovementDto Replay(StockMovement movement)
        {
            if (movement.InventoryItemId != request.InventoryItemId ||
                movement.Delta != request.Delta ||
                movement.Reason != request.Reason.Trim())
            {
                throw new ConflictException("OperationId was already used with different content.");
            }

            return MovementDto.FromEntity(movement);
        }

        _ = CatalogRules.Required(request.Reason, 1000, "Reason");
        if (request.OperationId == Guid.Empty)
        {
            throw new DomainException("OperationId is required.");
        }

        var existing = await ExistingAsync();
        if (existing is not null)
        {
            return Replay(existing);
        }

        try
        {
            return await work.ExecuteAsync(async () =>
            {
                // Separate reads release their read locks before looking up the parent.
                var variantId = await db.InventoryItems
                    .Where(i => i.Id == request.InventoryItemId)
                    .Select(i => (Guid?)i.VariantId)
                    .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Inventory item not found.");
                var productId = await db.ProductVariants
                    .Where(v => v.Id == variantId)
                    .Select(v => v.ProductId)
                    .SingleAsync(ct);
                var source = await db.Products
                    .Where(p => p.Id == productId)
                    .Select(p => new { p.Id, p.ProductTypeId })
                    .SingleAsync(ct);

                // Ordering locks type -> product -> balance too; never read a locked product while holding only its balance.
                await CommerceConcurrency.LockProductTypeAsync(db, source.ProductTypeId, ct);
                await db.Products
                    .Where(p => p.Id == source.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Version, p => p.Version), ct);
                if (!await db.Products.AnyAsync(p => p.Id == source.Id && p.FulfillmentKind == ProductFulfillmentKind.Physical, ct))
                {
                    throw new ConflictException("Only physical SKUs use warehouse adjustments.");
                }

                if (await db.InventoryItems
                        .Where(i => i.Id == request.InventoryItemId)
                        .ExecuteUpdateAsync(s => s.SetProperty(i => i.Version, i => i.Version), ct) != 1)
                {
                    throw new NotFoundException("Inventory item not found.");
                }

                var prior = await ExistingAsync();
                if (prior is not null)
                {
                    return Replay(prior);
                }

                var item = await db.InventoryItems.SingleAsync(i => i.Id == request.InventoryItemId, ct);
                CommerceConcurrency.CheckVersion(request.Version, item.Version);
                var movement = item.Adjust(request.OperationId, request.Delta, request.Reason, actor.GetUserId());
                db.StockMovements.Add(movement);
                return MovementDto.FromEntity(movement);
            }, ct);
        }
        catch (DbUpdateException)
        {
            var prior = await ExistingAsync();
            if (prior is not null)
            {
                return Replay(prior);
            }

            throw;
        }
    }
}
