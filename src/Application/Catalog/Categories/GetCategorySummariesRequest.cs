using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Catalog;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Catalog.Categories;

public sealed record GetCategorySummariesRequest : IRequest<List<CategorySummary>>;

public sealed class GetCategorySummariesRequestHandler(IApplicationDbContext db)
    : IRequestHandler<GetCategorySummariesRequest, List<CategorySummary>>
{
    public Task<List<CategorySummary>> Handle(GetCategorySummariesRequest request, CancellationToken ct) =>
        db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .Select(c => new CategorySummary(
                c.Id,
                db.Set<ProductCategory>().Count(pc => pc.CategoryId == c.Id && db.Products.Any(p => p.Id == pc.ProductId && !p.IsDeleted))))
            .ToListAsync(ct);
}
