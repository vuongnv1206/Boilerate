using Mapster;
using Boilerate.Domain.Catalog;

namespace Boilerate.Application.Catalog.Products;

public class ProductDto : IRegister
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ImagePath { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = default!;

    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Product, ProductDto>()
            .Map(dest => dest.CategoryName, src => src.Category.Name);
    }
}
