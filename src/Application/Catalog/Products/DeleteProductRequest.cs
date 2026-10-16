using Boilerate.Application.Common.Caching;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Catalog;
using MediatR;

namespace Boilerate.Application.Catalog.Products;

public class DeleteProductRequest : IRequest<Guid>
{
    public Guid Id { get; set; }

    public DeleteProductRequest(Guid id) => Id = id;
}

public class DeleteProductRequestHandler : IRequestHandler<DeleteProductRequest, Guid>
{
    private readonly IRepositoryWithEvents<Product> _repository;
    private readonly ICacheService _cache;

    public DeleteProductRequestHandler(IRepositoryWithEvents<Product> repository, ICacheService cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async Task<Guid> Handle(DeleteProductRequest request, CancellationToken cancellationToken)
    {
        var product = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Sản phẩm với Id {request.Id} không tồn tại.");

        // Không xóa File bằng _fileStorage khi xóa Product bởi vì Product được xóa mềm (SoftDelete),
        // thông tin file Path vẫn còn có thể coi lại từ giao diện khi Product đã được khôi phục.

        await _repository.DeleteAsync(product, cancellationToken);
        await _cache.RemoveAsync($"Product:{request.Id}", cancellationToken);

        return request.Id;
    }
}


