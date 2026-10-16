using Mapster;
using Boilerate.Domain.Catalog;

namespace Boilerate.Application.Catalog.Categories;

public class CategoryDto : IRegister
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }

    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Category, CategoryDto>();
    }
}
