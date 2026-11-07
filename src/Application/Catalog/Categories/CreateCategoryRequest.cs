using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Domain.Catalog;
using FluentValidation;
using MediatR;

namespace Boilerate.Application.Catalog.Categories;

public sealed record CreateCategoryRequest(
    string Name,
    string Slug,
    string? Description,
    Guid? ParentId,
    int SortOrder,
    bool IsActive,
    Guid? Id = null,
    string? Version = null) : IRequest<Category>;

public sealed class CreateCategoryRequestValidator : CustomValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Id).Null();
    }
}

public sealed class CreateCategoryRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work) : IRequestHandler<CreateCategoryRequest, Category>
{
    public Task<Category> Handle(CreateCategoryRequest request, CancellationToken ct) =>
        work.ExecuteAsync(async () =>
        {
            await CommerceConcurrency.AcquireGateAsync(db, "categories", ct);
            var category = new Category(request.Name, request.Description, request.Slug);
            var ancestors = await CategoryHierarchyHelper.ResolveAncestorsAsync(db, request.ParentId, ct);
            category.Edit(request.Name, request.Slug, request.Description, request.ParentId, request.SortOrder, request.IsActive, ancestors);
            db.Categories.Add(category);
            return category;
        }, ct);
}
