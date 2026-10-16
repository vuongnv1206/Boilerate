using Boilerate.Application.Common.Caching;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Catalog.Products;
using Boilerate.Domain.Catalog;
using MediatR;

namespace Boilerate.Application.Catalog.Categories;

public class DeleteCategoryRequest : IRequest<Guid>
{
    public Guid Id { get; set; }

    public DeleteCategoryRequest(Guid id) => Id = id;
}

public class DeleteCategoryRequestHandler : IRequestHandler<DeleteCategoryRequest, Guid>
{
    private readonly IRepositoryWithEvents<Category> _repository;
    private readonly IReadRepository<Product> _productRepo;
    private readonly ICacheService _cache;

    public DeleteCategoryRequestHandler(IRepositoryWithEvents<Category> repository, IReadRepository<Product> productRepo, ICacheService cache)
    {
        _repository = repository;
        _productRepo = productRepo;
        _cache = cache;
    }

    public async Task<Guid> Handle(DeleteCategoryRequest request, CancellationToken cancellationToken)
    {
        if (await _productRepo.AnyAsync(new ProductsByCategoryIdSpec(request.Id), cancellationToken))
            throw new ConflictException("Không thể xóa danh mục đang có sản phẩm.");

        var category = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Danh mục với Id {request.Id} không tồn tại.");

        await _repository.DeleteAsync(category, cancellationToken);
        await _cache.RemoveAsync($"Category:{request.Id}", cancellationToken);

        return request.Id;
    }
}
