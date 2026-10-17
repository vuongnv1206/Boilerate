using Boilerate.Domain.Common.Contracts;
using Boilerate.Domain.Common.Exceptions;
namespace Boilerate.Domain.Catalog;

public sealed class Category : CommerceEntity, IAggregateRoot
{
    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public string NormalizedSlug { get; private set; } = null!;
    public string? Description
    {
        get; private set;
    }
    public Guid? ParentId
    {
        get; private set;
    }
    public int SortOrder
    {
        get; private set;
    }
    public bool IsActive { get; private set; } = true;
    private Category()
    {
    }
    public Category(string name, string? description, string? slug = null)
        => Edit(name, slug ?? Id.ToString("N"), description, null, 0, true, []);
    public void Edit(string name, string slug, string? description, Guid? parentId, int sortOrder, bool active, IReadOnlyCollection<Guid> ancestors)
    {
        if (parentId == Id || ancestors.Contains(Id))
            throw new DomainException("Category hierarchy cannot contain a cycle.");
        if (sortOrder < 0 || description?.Length > 4000)
            throw new DomainException("Invalid category data.");
        Name = CatalogRules.Required(name, 256, "Name");
        Slug = CatalogRules.Slug(slug);
        NormalizedSlug = Slug;
        Description = description;
        ParentId = parentId;
        SortOrder = sortOrder;
        IsActive = active;
        Touch();
    }
}