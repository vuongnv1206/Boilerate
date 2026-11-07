using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Identity.Users;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Inventory.Digital;

public sealed record GetDigitalStockRequest(Guid VariantId, int Page = 1, int Size = 20)
    : IRequest<Page<DigitalStockDto>>;

public sealed class GetDigitalStockRequestHandler(
    IApplicationDbContext db,
    ICurrentUser actor,
    IUserService users) : IRequestHandler<GetDigitalStockRequest, Page<DigitalStockDto>>
{
    public async Task<Page<DigitalStockDto>> Handle(GetDigitalStockRequest request, CancellationToken ct)
    {
        await DigitalInventoryHelper.RequirePermissionAsync(users, actor, "View", ct);
        CommerceConcurrency.ValidatePage(request.Page, request.Size);
        var product = await db.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(p => !p.IsDeleted && p.Variants.Any(v => v.Id == request.VariantId), ct)
            ?? throw new NotFoundException("SKU not found.");
        if (product.FulfillmentKind is not ProductFulfillmentKind.DigitalUnique and not ProductFulfillmentKind.DigitalShared)
        {
            throw new DomainException("This SKU does not use digital stock.");
        }

        var query = db.DigitalStockItems.AsNoTracking().Where(i => i.VariantId == request.VariantId);
        int total = await query.CountAsync(ct);

        // Project metadata in SQL so ciphertext is never materialized for a list response.
        var rows = await query
            .OrderBy(i => i.Id)
            .Skip((request.Page - 1) * request.Size)
            .Take(request.Size)
            .Select(i => new DigitalStockDto(i.Id, i.ProductId, i.VariantId, i.FulfillmentKind, i.Reference, i.State, i.Version, product.Version))
            .ToListAsync(ct);
        return new(rows, total, request.Page, request.Size);
    }
}
