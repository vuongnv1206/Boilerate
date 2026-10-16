using Ardalis.Specification;
using Boilerate.Application.Common.Specification;
using Boilerate.Application.Common.Specifications;
using Boilerate.Application.Common.Models;
using Boilerate.Domain.Catalog;

namespace Boilerate.Application.Catalog.Products;

public class ProductByIdSpec : Specification<Product>, ISingleResultSpecification<Product>
{
    public ProductByIdSpec(Guid id) =>
        Query
            .Where(p => p.Id == id)
            .Include(p => p.Category);
}

public class ProductByNameSpec : Specification<Product>, ISingleResultSpecification<Product>
{
    public ProductByNameSpec(string name) =>
        Query.Where(p => p.Name == name);
}

public class ProductsBySearchRequestSpec : EntitiesByPaginationFilterSpec<Product>
{
    public ProductsBySearchRequestSpec(SearchProductsRequest request)
        : base(request) =>
        Query
            .Include(p => p.Category)
            .Where(p => p.CategoryId == request.CategoryId, request.CategoryId.HasValue)
            .Where(p => p.Price >= request.MinimumPrice, request.MinimumPrice.HasValue)
            .Where(p => p.Price <= request.MaximumPrice, request.MaximumPrice.HasValue)
            .OrderBy(c => c.Name, !request.HasOrderBy());
}

public class ProductsByCategoryIdSpec : Specification<Product>
{
    public ProductsByCategoryIdSpec(Guid categoryId) =>
        Query.Where(p => p.CategoryId == categoryId);
}

public class DeletedProductByIdSpec : SoftDeleteSpecification<Product>, ISingleResultSpecification<Product>
{
    public DeletedProductByIdSpec(Guid id)
    {
        Query
            .Include(p => p.Category)
            .Where(p => p.Id == id);
        OnlyDeleted();
    }
}

public class OnlyDeletedProductsSpec : SoftDeleteSpecification<Product>
{
    public OnlyDeletedProductsSpec()
    {
        Query.Include(p => p.Category);
        OnlyDeleted();
    }
}

public class AllProductsIncludingDeletedSpec : SoftDeleteSpecification<Product>
{
    public AllProductsIncludingDeletedSpec()
    {
        Query.Include(p => p.Category);
        IncludeDeleted();
    }
}

public class DeletedProductsByDateRangeSpec : SoftDeleteSpecification<Product>
{
    public DeletedProductsByDateRangeSpec(DateTime from, DateTime to)
    {
        Query.Include(p => p.Category);
        OnlyDeleted();
        Query.Where(e => e.DeletedOn >= from && e.DeletedOn <= to);
    }
}

public class DeletedProductsSpec : EntitiesByPaginationFilterSpec<Product>
{
    public DeletedProductsSpec(PaginationFilter request) : base(request) =>
        Query
            .Include(p => p.Category)
            .Where(p => p.DeletedOn != null)
            .IgnoreQueryFilters();
}
