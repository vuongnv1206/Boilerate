using Boilerate.Application.Common.Models;
using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Catalog;
using Mapster;
using MediatR;

namespace Boilerate.Application.Catalog.Categories;

public class SearchCategoriesRequest : PaginationFilter, IRequest<PaginationResponse<CategoryDto>>
{
}

public class SearchCategoriesRequestHandler : IRequestHandler<SearchCategoriesRequest, PaginationResponse<CategoryDto>>
{
    private readonly IReadRepository<Category> _repository;

    public SearchCategoriesRequestHandler(IReadRepository<Category> repository) => _repository = repository;

    public async Task<PaginationResponse<CategoryDto>> Handle(SearchCategoriesRequest request, CancellationToken cancellationToken)
    {
        var spec = new CategoriesBySearchRequestSpec(request);
        
        var list = await _repository.ListAsync(spec, cancellationToken);
        var count = await _repository.CountAsync(spec, cancellationToken);
        
        var categoryDtos = list.Adapt<List<CategoryDto>>();

        return new PaginationResponse<CategoryDto>(categoryDtos, count, request.PageNumber, request.PageSize);
    }
}
