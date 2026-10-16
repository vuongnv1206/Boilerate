using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Catalog;
using MediatR;

namespace Boilerate.Application.Catalog.Products;

public class PermanentDeleteProductRequest : IRequest<Guid>
{
    public Guid Id { get; set; }
    public PermanentDeleteProductRequest(Guid id) => Id = id;
}

public class PermanentDeleteProductRequestHandler : IRequestHandler<PermanentDeleteProductRequest, Guid>
{
    private readonly IRepositoryWithEvents<Product> _repository;
    private readonly IPermanentDeleteService<Product> _permanentDeleteService;
    
    public PermanentDeleteProductRequestHandler(
        IRepositoryWithEvents<Product> repository, 
        IPermanentDeleteService<Product> permanentDeleteService)
    {
        _repository = repository;
        _permanentDeleteService = permanentDeleteService;
    }

    public async Task<Guid> Handle(PermanentDeleteProductRequest request, CancellationToken cancellationToken)
    {
        // Phải lờ đi Global Filter để lấy được item đã xóa mềm.
        var product = await _repository.FirstOrDefaultAsync(new DeletedProductByIdSpec(request.Id), cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy sản phẩm bị xóa mềm với Id {request.Id}. Chỉ có thể xóa vĩnh viễn các sản phẩm đã bị xóa mềm.");

        await _permanentDeleteService.PermanentDeleteAsync(product, cancellationToken);
        
        return request.Id;
    }
}
