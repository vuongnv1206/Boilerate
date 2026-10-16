using Boilerate.Domain.Common.Contracts;

namespace Boilerate.Domain.Catalog;

/// <summary>
/// Domain entity đại diện cho danh mục sản phẩm (Category).
/// Kế thừa AuditableEntity đã bao gồm Id (Guid), Created/Modified tracking, và Soft Delete.
/// </summary>
public class Category : AuditableEntity, IAggregateRoot
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }

    public Category(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    // EF Core constructor
    protected Category() { }
}
