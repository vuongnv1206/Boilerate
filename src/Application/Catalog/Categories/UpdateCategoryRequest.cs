using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Domain.Catalog;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Catalog.Categories;

public sealed record UpdateCategoryRequest(
    Guid? Id,
    string? Version,
    string Name,
    string Slug,
    string? Description,
    Guid? ParentId,
    int SortOrder,
    bool IsActive) : IRequest<Category>;

public sealed class UpdateCategoryRequestValidator : CustomValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(x => x.Id).NotNull().NotEqual(Guid.Empty);
    }
}

public sealed class UpdateCategoryRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work) : IRequestHandler<UpdateCategoryRequest, Category>
{
    public Task<Category> Handle(UpdateCategoryRequest request, CancellationToken ct) =>
        work.ExecuteAsync(async () =>
        {
            await CommerceConcurrency.AcquireGateAsync(db, "categories", ct);
            var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == request.Id, ct)
                ?? throw new NotFoundException("Category not found.");
            CommerceConcurrency.CheckVersion(request.Version, category.Version);
            var ancestors = await CategoryHierarchyHelper.ResolveAncestorsAsync(db, request.ParentId, ct);
            category.Edit(request.Name, request.Slug, request.Description, request.ParentId, request.SortOrder, request.IsActive, ancestors);
            return category;
        }, ct);
}
