using Boilerate.Application.Catalog;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Infrastructure.Commerce;

public sealed partial class CommerceService
{
    public Task<List<CategoryProductResult>> SetCategoryProductsAsync(SetCategoryProducts request, CancellationToken ct)
    {
        if (request.CategoryId == Guid.Empty || request.Changes is null || request.Changes.Count > 1000 ||
            request.Changes.Any(change => change is null || change.ProductId == Guid.Empty || string.IsNullOrWhiteSpace(change.Version)) ||
            request.Changes.Select(change => change.ProductId).Distinct().Count() != request.Changes.Count)
            throw new DomainException("Invalid category product selection.");

        return work.ExecuteAsync(async () =>
        {
            await Gate("categories", ct);
            if (!await db.Categories.AnyAsync(category => category.Id == request.CategoryId, ct))
                throw new NotFoundException("Category not found.");

            if (request.Changes.Count == 0)
                return [];

            var ids = request.Changes.Select(change => change.ProductId).ToArray();
            var owners = await db.Products.Where(product => ids.Contains(product.Id) && !product.IsDeleted)
                .Select(product => new { product.Id, product.ProductTypeId })
                .ToListAsync(ct);
            if (owners.Count != ids.Length)
                throw new NotFoundException("A selected product no longer exists; reload the selection.");

            // Match Product writes: acquire type locks in stable order, then product row locks.
            foreach (var typeId in owners.Select(owner => owner.ProductTypeId).Distinct().Order())
                await LockedType(typeId, ct);

            var changes = request.Changes.ToDictionary(change => change.ProductId);
            var products = new Dictionary<Guid, Product>();
            foreach (var owner in owners.OrderBy(owner => owner.ProductTypeId).ThenBy(owner => owner.Id))
            {
                if (await db.Products.Where(product => product.Id == owner.Id && !product.IsDeleted)
                        .ExecuteUpdateAsync(update => update.SetProperty(product => product.Version, product => product.Version), ct) != 1)
                    throw new NotFoundException("A selected product no longer exists; reload the selection.");

                var product = await db.Products.Include(product => product.Categories)
                    .SingleAsync(product => product.Id == owner.Id, ct);
                Version(changes[owner.Id].Version, product.Version);
                product.SetCategoryMembership(request.CategoryId, changes[owner.Id].Included);
                products.Add(product.Id, product);
            }

            return request.Changes.Select(change =>
                new CategoryProductResult(change.ProductId, products[change.ProductId].Version, change.Included)).ToList();
        }, ct);
    }
}
