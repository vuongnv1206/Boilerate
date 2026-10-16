using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Extensions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Catalog;
using MediatR;

namespace Boilerate.Application.Catalog.Products;

public class RestoreProductRequest : IRequest<Guid>
{
    public Guid Id { get; set; }
    public RestoreProductRequest(Guid id) => Id = id;
}

public class RestoreProductRequestHandler : IRequestHandler<RestoreProductRequest, Guid>
{
    private readonly IRepositoryWithEvents<Product> _repository;
    
    public RestoreProductRequestHandler(IRepositoryWithEvents<Product> repository) => _repository = repository;

    public async Task<Guid> Handle(RestoreProductRequest request, CancellationToken cancellationToken)
    {
        var product = await _repository.FirstOrDefaultAsync(new DeletedProductByIdSpec(request.Id), cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy sản phẩm bị xóa với Id {request.Id}.");

        product.Restore(); // Using extension
        await _repository.UpdateAsync(product, cancellationToken);
        
        return request.Id;
    }
}
