using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Catalog.Categories;

public sealed record CategorySummary(Guid Id, int ProductCount);

public sealed record CategoryProductChange(Guid ProductId, string Version, bool Included);

public sealed record CategoryProductResult(Guid ProductId, string Version, bool Included);

public sealed record PublicCategoryDto(
    Guid Id,
    Guid? ParentId,
    string Name,
    string Slug,
    string? Description,
    int SortOrder);

internal static class CategoryHierarchyHelper
{
    internal static async Task<HashSet<Guid>> ResolveAncestorsAsync(
        IApplicationDbContext db,
        Guid? parentId,
        CancellationToken ct)
    {
        var ancestors = new HashSet<Guid>();
        Guid? current = parentId;
        while (current.HasValue)
        {
            if (!ancestors.Add(current.Value))
            {
                throw new ConflictException("Existing category cycle.");
            }

            var node = await db.Categories.SingleOrDefaultAsync(c => c.Id == current.Value, ct)
                ?? throw new DomainException("Unknown parent.");
            current = node.ParentId;
        }

        return ancestors;
    }
}
