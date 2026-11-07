using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Catalog;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Catalog.ProductTypes;

public sealed record GetProductTypesRequest : IRequest<List<ProductType>>;

public sealed class GetProductTypesRequestHandler(IApplicationDbContext db)
    : IRequestHandler<GetProductTypesRequest, List<ProductType>>
{
    public Task<List<ProductType>> Handle(GetProductTypesRequest request, CancellationToken ct) =>
        db.ProductTypes.AsNoTracking().OrderBy(t => t.Id).ToListAsync(ct);
}
