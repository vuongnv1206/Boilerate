using Boilerate.Application.Common.Models;
using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Catalog;
using Mapster;
using MediatR;

namespace Boilerate.Application.Catalog.Products;

public class SearchProductsRequest : PaginationFilter, IRequest<PaginationResponse<ProductDto>>
{
    public Guid? CategoryId { get; set; }
    public decimal? MinimumPrice { get; set; }
    public decimal? MaximumPrice { get; set; }
}

public class SearchProductsRequestHandler : IRequestHandler<SearchProductsRequest, PaginationResponse<ProductDto>>
{
    private readonly IReadRepository<Product> _repository;

    public SearchProductsRequestHandler(IReadRepository<Product> repository) => _repository = repository;

    public async Task<PaginationResponse<ProductDto>> Handle(SearchProductsRequest request, CancellationToken cancellationToken)
    {
        var spec = new ProductsBySearchRequestSpec(request);
        
        var list = await _repository.ListAsync(spec, cancellationToken);
        var count = await _repository.CountAsync(spec, cancellationToken);
        
        var productDtos = list.Adapt<List<ProductDto>>();

        return new PaginationResponse<ProductDto>(productDtos, count, request.PageNumber, request.PageSize);
    }
}
