using Boilerate.Application.Common.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Inventory.Physical;

public sealed record GetStockMovementsRequest(Guid InventoryItemId, int Page = 1, int Size = 20)
    : IRequest<Page<MovementDto>>;

public sealed class GetStockMovementsRequestHandler(IApplicationDbContext db)
    : IRequestHandler<GetStockMovementsRequest, Page<MovementDto>>
{
    public async Task<Page<MovementDto>> Handle(GetStockMovementsRequest request, CancellationToken ct)
    {
        CommerceConcurrency.ValidatePage(request.Page, request.Size);
        var query = db.StockMovements.AsNoTracking().Where(m => m.InventoryItemId == request.InventoryItemId);
        var rows = await query
            .OrderByDescending(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Skip((request.Page - 1) * request.Size)
            .Take(request.Size)
            .ToListAsync(ct);
        int total = await query.CountAsync(ct);
        return new(rows.Select(MovementDto.FromEntity).ToList(), total, request.Page, request.Size);
    }
}
