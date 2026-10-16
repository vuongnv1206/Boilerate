using Ardalis.Specification;
using Boilerate.Application.Common.Specification;
using Boilerate.Application.Common.Models;
using Boilerate.Domain.Catalog;

namespace Boilerate.Application.Catalog.Categories;

public class CategoryByIdSpec : Specification<Category>, ISingleResultSpecification<Category>
{
    public CategoryByIdSpec(Guid id) =>
        Query.Where(c => c.Id == id);
}

public class CategoryByNameSpec : Specification<Category>, ISingleResultSpecification<Category>
{
    public CategoryByNameSpec(string name) =>
        Query.Where(c => c.Name == name);
}

public class CategoriesBySearchRequestSpec : EntitiesByPaginationFilterSpec<Category>
{
    public CategoriesBySearchRequestSpec(SearchCategoriesRequest request)
        : base(request) =>
        Query.OrderBy(c => c.Name, !request.HasOrderBy());
}
