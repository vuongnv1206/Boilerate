using Boilerate.Application.Common.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Inventory.Physical;

public sealed record GetInventoryItemsRequest(
    int Page = 1,
    int Size = 20,
    Guid? ProductId = null,
    Guid? VariantId = null) : IRequest<Page<InventoryDto>>;

public sealed class GetInventoryItemsRequestHandler(IApplicationDbContext db)
    : IRequestHandler<GetInventoryItemsRequest, Page<InventoryDto>>
{
    public async Task<Page<InventoryDto>> Handle(GetInventoryItemsRequest request, CancellationToken ct)
    {
        CommerceConcurrency.ValidatePage(request.Page, request.Size);
        var query = db.InventoryItems.AsNoTracking();
        if (request.ProductId.HasValue)
        {
            query = query.Where(i => db.ProductVariants.Any(v => v.Id == i.VariantId && v.ProductId == request.ProductId));
        }

        if (request.VariantId.HasValue)
        {
            query = query.Where(i => i.VariantId == request.VariantId);
        }

        var items = await query
            .OrderBy(i => i.Id)
            .Skip((request.Page - 1) * request.Size)
            .Take(request.Size)
            .Select(i => new InventoryDto(i.Id, i.VariantId, i.LocationId, i.OnHand, i.Version, i.Reserved))
            .ToListAsync(ct);
        int total = await query.CountAsync(ct);
        return new(items, total, request.Page, request.Size);
    }
}
