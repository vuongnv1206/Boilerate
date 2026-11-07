using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Domain.Catalog;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Catalog.Categories;

public sealed record DeleteCategoryRequest(Guid Id, string Version) : IRequest<bool>;

public sealed class DeleteCategoryRequestValidator : CustomValidator<DeleteCategoryRequest>
{
    public DeleteCategoryRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class DeleteCategoryRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work) : IRequestHandler<DeleteCategoryRequest, bool>
{
    public Task<bool> Handle(DeleteCategoryRequest request, CancellationToken ct) =>
        work.ExecuteAsync(async () =>
        {
            await CommerceConcurrency.AcquireGateAsync(db, "categories", ct);
            var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == request.Id, ct)
                ?? throw new NotFoundException("Category not found.");
            CommerceConcurrency.CheckVersion(request.Version, category.Version);
            if (await db.Categories.AnyAsync(c => c.ParentId == request.Id, ct) ||
                await db.Set<ProductCategory>().AnyAsync(c => c.CategoryId == request.Id, ct))
            {
                throw new ConflictException("Category is still referenced.");
            }

            db.Categories.Remove(category);
            return true;
        }, ct);
}
