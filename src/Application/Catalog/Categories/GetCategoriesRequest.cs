using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Catalog;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Catalog.Categories;

public sealed record GetCategoriesRequest(bool ActiveOnly = false) : IRequest<List<Category>>;

public sealed class GetCategoriesRequestHandler(IApplicationDbContext db)
    : IRequestHandler<GetCategoriesRequest, List<Category>>
{
    public Task<List<Category>> Handle(GetCategoriesRequest request, CancellationToken ct) =>
        db.Categories
            .AsNoTracking()
            .Where(c => !request.ActiveOnly || c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Id)
            .ToListAsync(ct);
}
