using Boilerate.Application.Common.Models;
using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Catalog;
using Mapster;
using MediatR;

namespace Boilerate.Application.Catalog.Products;

public class GetDeletedProductsRequest : PaginationFilter, IRequest<PaginationResponse<ProductDto>>
{
}

public class GetDeletedProductsRequestHandler : IRequestHandler<GetDeletedProductsRequest, PaginationResponse<ProductDto>>
{
    private readonly IReadRepository<Product> _repository;

    public GetDeletedProductsRequestHandler(IReadRepository<Product> repository) => _repository = repository;

    public async Task<PaginationResponse<ProductDto>> Handle(GetDeletedProductsRequest request, CancellationToken cancellationToken)
    {
        var spec = new DeletedProductsSpec(request);
        
        var list = await _repository.ListAsync(spec, cancellationToken);
        var count = await _repository.CountAsync(spec, cancellationToken);
        
        var dtos = list.Adapt<List<ProductDto>>();
        
        return new PaginationResponse<ProductDto>(dtos, count, request.PageNumber, request.PageSize);
    }
}
