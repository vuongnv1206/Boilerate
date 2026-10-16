using Boilerate.Domain.Common.Contracts;

namespace Boilerate.Domain.Catalog;

/// <summary>
/// Domain entity đại diện cho sản phẩm (Product).
/// Kế thừa AuditableEntity đã bao gồm Id (Guid), Created/Modified tracking, và Soft Delete.
/// </summary>
public class Product : AuditableEntity, IAggregateRoot
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public decimal Price { get; set; } // Using Rate as the price property
    public string? ImagePath { get; set; }
    
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = default!;

    public Product(string name, string? description, decimal price, Guid categoryId, string? imagePath)
    {
        Name = name;
        Description = description;
        Price = price;
        CategoryId = categoryId;
        ImagePath = imagePath;
    }

    // EF Core constructor
    protected Product() { }
}
