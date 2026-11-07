using Boilerate.Application.Catalog.Categories;
using Boilerate.Application.Common.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Storefront.PublicCatalog;

public sealed record GetPublicCategoriesRequest : IRequest<List<PublicCategoryDto>>;

public sealed class GetPublicCategoriesRequestHandler(IApplicationDbContext db)
    : IRequestHandler<GetPublicCategoriesRequest, List<PublicCategoryDto>>
{
    public Task<List<PublicCategoryDto>> Handle(GetPublicCategoriesRequest request, CancellationToken ct) =>
        db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Id)
            .Select(c => new PublicCategoryDto(c.Id, c.ParentId, c.Name, c.Slug, c.Description, c.SortOrder))
            .ToListAsync(ct);
}

