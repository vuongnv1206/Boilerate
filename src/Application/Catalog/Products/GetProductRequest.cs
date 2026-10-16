using Boilerate.Application.Common.Caching;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Catalog;
using Mapster;
using MediatR;

namespace Boilerate.Application.Catalog.Products;

public class GetProductRequest : IRequest<ProductDto>
{
    public Guid Id { get; set; }

    public GetProductRequest(Guid id) => Id = id;
}

public class GetProductRequestHandler : IRequestHandler<GetProductRequest, ProductDto>
{
    private readonly IReadRepository<Product> _repository;
    private readonly ICacheService _cache;

    public GetProductRequestHandler(IReadRepository<Product> repository, ICacheService cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async Task<ProductDto> Handle(GetProductRequest request, CancellationToken cancellationToken)
    {
        var cacheKey = $"Product:{request.Id}";
        var productDto = await _cache.GetAsync<ProductDto>(cacheKey, cancellationToken);

        if (productDto == null)
        {
            var product = await _repository.FirstOrDefaultAsync(new ProductByIdSpec(request.Id), cancellationToken)
                ?? throw new NotFoundException($"Không tìm thấy sản phẩm với Id {request.Id}.");

            productDto = product.Adapt<ProductDto>();
            await _cache.SetAsync(cacheKey, productDto, TimeSpan.FromMinutes(30), cancellationToken);
        }

        return productDto;
    }
}
