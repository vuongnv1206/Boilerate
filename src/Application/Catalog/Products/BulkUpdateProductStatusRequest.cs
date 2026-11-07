using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Catalog.Products;

public sealed record BulkUpdateProductStatusRequest(
    List<ProductVersionInput> Items,
    ProductStatus Status) : IRequest<BulkProductStatusResult>;

public sealed class BulkUpdateProductStatusRequestValidator : CustomValidator<BulkUpdateProductStatusRequest>
{
    public BulkUpdateProductStatusRequestValidator()
    {
        RuleFor(x => x.Items).NotEmpty().Must(x => x is null || x.Count <= 100);
        RuleForEach(x => x.Items).NotNull().ChildRules(item =>
        {
            item.RuleFor(x => x.Id).NotEmpty();
            item.RuleFor(x => x.Version).NotEmpty();
        });
        RuleFor(x => x.Status).Must(x => x is ProductStatus.Published or ProductStatus.Archived);
    }
}

public sealed class BulkUpdateProductStatusRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work) : IRequestHandler<BulkUpdateProductStatusRequest, BulkProductStatusResult>
{
    public Task<BulkProductStatusResult> Handle(BulkUpdateProductStatusRequest request, CancellationToken ct)
    {
        if (request.Items is null ||
            request.Items.Count is < 1 or > 100 ||
            request.Items.Any(i => i is null || i.Id == Guid.Empty || string.IsNullOrWhiteSpace(i.Version)) ||
            request.Items.Select(i => i.Id).Distinct().Count() != request.Items.Count ||
            request.Status is not (ProductStatus.Published or ProductStatus.Archived))
        {
            throw new DomainException("Select 1–100 distinct products and Published or Archived.");
        }

        return work.ExecuteAsync(async () =>
        {
            var ids = request.Items.Select(i => i.Id).ToArray();
            var owners = await db.Products
                .Where(p => ids.Contains(p.Id))
                .Select(p => new { p.Id, p.ProductTypeId })
                .ToListAsync(ct);
            if (owners.Count != ids.Length)
            {
                throw new NotFoundException("A selected product no longer exists; reload the selection.");
            }

            var types = new Dictionary<Guid, ProductType>();
            foreach (Guid typeId in owners.Select(p => p.ProductTypeId).Distinct().Order())
            {
                types.Add(typeId, await CommerceConcurrency.LockProductTypeAsync(db, typeId, ct));
            }

            var versions = request.Items.ToDictionary(i => i.Id, i => i.Version);
            var products = new Dictionary<Guid, Product>();
            foreach (Guid id in ids.Order())
            {
                await db.Products
                    .Where(p => p.Id == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Version, p => p.Version), ct);
                var product = await ProductPersistenceHelper.ProductGraph(db).SingleAsync(p => p.Id == id, ct);
                if (versions[id] != product.Version)
                {
                    throw new ConflictException($"Product {id} changed; reload the selection.");
                }

                products.Add(id, product);
            }

            foreach (var product in products.Values)
            {
                try
                {
                    product.ChangeStatus(request.Status, types[product.ProductTypeId]);
                    product.Validate(types[product.ProductTypeId]);
                }
                catch (DomainException ex)
                {
                    throw new DomainException($"Product {product.Id}: {ex.Message}");
                }
            }

            return new BulkProductStatusResult(request.Items
                .Select(i => new ProductStatusResult(i.Id, products[i.Id].Status, products[i.Id].Version))
                .ToList());
        }, ct);
    }
}

