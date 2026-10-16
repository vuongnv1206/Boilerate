using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Catalog;
using FluentValidation;
using MediatR;

namespace Boilerate.Application.Catalog.Categories;

public class CreateCategoryRequest : IRequest<Guid>
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
}

public class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator(IReadRepository<Category> repository)
    {
        RuleFor(p => p.Name)
            .NotEmpty()
            .MaximumLength(75)
            .MustAsync(async (name, ct) => await repository.FirstOrDefaultAsync(new CategoryByNameSpec(name), ct) is null)
                .WithMessage((_, name) => $"Danh mục {name} đã tồn tại.");
    }
}

public class CreateCategoryRequestHandler : IRequestHandler<CreateCategoryRequest, Guid>
{
    private readonly IRepositoryWithEvents<Category> _repository;

    public CreateCategoryRequestHandler(IRepositoryWithEvents<Category> repository) => _repository = repository;

    public async Task<Guid> Handle(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = new Category(request.Name, request.Description);
        await _repository.AddAsync(category, cancellationToken);
        return category.Id;
    }
}
