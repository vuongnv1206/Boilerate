using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Catalog.Categories;

public sealed record SetCategoryProductsRequest(
    Guid CategoryId,
    List<CategoryProductChange> Changes) : IRequest<List<CategoryProductResult>>;

public sealed class SetCategoryProductsRequestValidator : CustomValidator<SetCategoryProductsRequest>
{
    public SetCategoryProductsRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Changes).NotNull().Must(x => x is { Count: <= 1000 });
        RuleForEach(x => x.Changes).NotNull().ChildRules(change =>
        {
            change.RuleFor(x => x.ProductId).NotEmpty();
            change.RuleFor(x => x.Version).NotEmpty();
        });
        RuleFor(x => x.Changes)
            .Must(x => x is null || x.Exists(change => change is null) ||
                x.Select(change => change.ProductId).Distinct().Count() == x.Count)
            .WithMessage("A product can appear only once in a category update.");
    }
}

public sealed class SetCategoryProductsRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work) : IRequestHandler<SetCategoryProductsRequest, List<CategoryProductResult>>
{
    public Task<List<CategoryProductResult>> Handle(SetCategoryProductsRequest request, CancellationToken ct)
    {
        if (request.CategoryId == Guid.Empty || request.Changes is null || request.Changes.Count > 1000 ||
            request.Changes.Any(change => change is null || change.ProductId == Guid.Empty || string.IsNullOrWhiteSpace(change.Version)) ||
            request.Changes.Select(change => change.ProductId).Distinct().Count() != request.Changes.Count)
        {
            throw new DomainException("Invalid category product selection.");
        }

        return work.ExecuteAsync(async () =>
        {
            await CommerceConcurrency.AcquireGateAsync(db, "categories", ct);
            if (!await db.Categories.AnyAsync(category => category.Id == request.CategoryId, ct))
            {
                throw new NotFoundException("Category not found.");
            }

            if (request.Changes.Count == 0)
            {
                return [];
            }

            var ids = request.Changes.Select(change => change.ProductId).ToArray();
            var owners = await db.Products
                .Where(product => ids.Contains(product.Id) && !product.IsDeleted)
                .Select(product => new { product.Id, product.ProductTypeId })
                .ToListAsync(ct);
            if (owners.Count != ids.Length)
            {
                throw new NotFoundException("A selected product no longer exists; reload the selection.");
            }

            // Match Product writes: acquire type locks in stable order, then product row locks.
            foreach (Guid typeId in owners.Select(owner => owner.ProductTypeId).Distinct().Order())
            {
                await CommerceConcurrency.LockProductTypeAsync(db, typeId, ct);
            }

            var changes = request.Changes.ToDictionary(change => change.ProductId);
            var products = new Dictionary<Guid, Product>();
            foreach (var owner in owners.OrderBy(owner => owner.ProductTypeId).ThenBy(owner => owner.Id))
            {
                if (await db.Products
                        .Where(product => product.Id == owner.Id && !product.IsDeleted)
                        .ExecuteUpdateAsync(update => update.SetProperty(product => product.Version, product => product.Version), ct) != 1)
                {
                    throw new NotFoundException("A selected product no longer exists; reload the selection.");
                }

                var product = await db.Products
                    .Include(product => product.Categories)
                    .SingleAsync(product => product.Id == owner.Id, ct);
                CommerceConcurrency.CheckVersion(changes[owner.Id].Version, product.Version);
                product.SetCategoryMembership(request.CategoryId, changes[owner.Id].Included);
                products.Add(product.Id, product);
            }

            return request.Changes
                .Select(change => new CategoryProductResult(change.ProductId, products[change.ProductId].Version, change.Included))
                .ToList();
        }, ct);
    }
}
