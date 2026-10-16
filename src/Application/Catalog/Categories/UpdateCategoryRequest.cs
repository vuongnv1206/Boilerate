using Boilerate.Application.Common.Caching;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Catalog;
using FluentValidation;
using MediatR;

namespace Boilerate.Application.Catalog.Categories;

public class UpdateCategoryRequest : IRequest<Guid>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
}

public class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator(IReadRepository<Category> repository)
    {
        RuleFor(p => p.Name)
            .NotEmpty()
            .MaximumLength(75)
            .MustAsync(async (req, name, ct) => 
                await repository.FirstOrDefaultAsync(new CategoryByNameSpec(name), ct)
                    is not Category existingCategory || existingCategory.Id == req.Id)
                .WithMessage((_, name) => $"Danh mục {name} đã tồn tại.");
    }
}

public class UpdateCategoryRequestHandler : IRequestHandler<UpdateCategoryRequest, Guid>
{
    private readonly IRepositoryWithEvents<Category> _repository;
    private readonly ICacheService _cache;

    public UpdateCategoryRequestHandler(IRepositoryWithEvents<Category> repository, ICacheService cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async Task<Guid> Handle(UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Danh mục với Id {request.Id} không tồn tại.");

        category.Name = request.Name;
        category.Description = request.Description;

        await _repository.UpdateAsync(category, cancellationToken);
        await _cache.RemoveAsync($"Category:{request.Id}", cancellationToken);

        return request.Id;
    }
}
