using Boilerate.Application.Common.FileStorage;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.BackgroundJobs;
using Boilerate.Application.Notifications;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common;
using Boilerate.Domain.Notifications;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Boilerate.Application.Catalog.Products;

public class CreateProductRequest : IRequest<Guid>
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public Guid CategoryId { get; set; }
    public Microsoft.AspNetCore.Http.IFormFile? Image { get; set; }
}

public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator(IReadRepository<Product> productRepo, IReadRepository<Category> categoryRepo)
    {
        RuleFor(p => p.Name)
            .NotEmpty()
            .MaximumLength(75)
            .MustAsync(async (name, ct) => await productRepo.FirstOrDefaultAsync(new ProductByNameSpec(name), ct) is null)
                .WithMessage((_, name) => $"Sản phẩm {name} đã tồn tại.");

        RuleFor(p => p.CategoryId)
            .NotEmpty()
            .MustAsync(async (id, ct) => await categoryRepo.GetByIdAsync(id, ct) is not null)
                .WithMessage((_, id) => $"Danh mục với Id {id} không tồn tại.");

        RuleFor(p => p.Price)
            .GreaterThanOrEqualTo(0);
    }
}

public class CreateProductRequestHandler : IRequestHandler<CreateProductRequest, Guid>
{
    private readonly IRepositoryWithEvents<Product> _repository;
    private readonly IFileStorageService _file;
    private readonly ILogger<CreateProductRequestHandler> _logger;

    public CreateProductRequestHandler(
        IRepositoryWithEvents<Product> repository, 
        IFileStorageService file, 
        ILogger<CreateProductRequestHandler> logger)
    {
        _repository = repository;
        _file = file;
        _logger = logger;
    }

    public async Task<Guid> Handle(CreateProductRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Đang bắt đầu tạo Sản phẩm mới: {Name}", request.Name);

        string? imagePath = await _file.UploadAsync<Product>(request.Image, FileType.Image, cancellationToken);
        var product = new Product(request.Name, request.Description, request.Price, request.CategoryId, imagePath);

        await _repository.AddAsync(product, cancellationToken);
        _logger.LogInformation("Đã lưu Database thành công cho Sản phẩm [{Id}]", product.Id);

        _logger.LogInformation("Hoàn tất tạo Sản phẩm {Id}.", product.Id);

        return product.Id;
    }
}
