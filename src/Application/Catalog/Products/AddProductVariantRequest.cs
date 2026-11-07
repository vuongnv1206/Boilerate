using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Inventory;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace Boilerate.Application.Catalog.Products;

public sealed record AddProductVariantRequest(
    Guid ProductId,
    string Version,
    VariantInput Variant) : IRequest<ProductDto>;

public sealed class AddProductVariantRequestValidator : CustomValidator<AddProductVariantRequest>
{
    public AddProductVariantRequestValidator()
    {
        RuleFor(x => x.Version).NotEmpty();
        RuleFor(x => x.Variant).NotNull().SetValidator(new VariantInputValidator());
    }
}

public sealed class AddProductVariantRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IUserService users,
    IOptions<CommerceSettings> settings) : IRequestHandler<AddProductVariantRequest, ProductDto>
{
    public async Task<ProductDto> Handle(AddProductVariantRequest request, CancellationToken ct)
    {
        if (request.Variant.Price.HasValue || request.Variant.CompareAtPrice.HasValue)
        {
            await ProductPersistenceHelper.RequirePricePermissionAsync(users, actor, ct);
        }

        int maxVariants = settings.Value.MaximumVariants;
        await work.ExecuteAsync(async () =>
        {
            var (product, type) = await ProductPersistenceHelper.LockProductAsync(db, request.ProductId, request.Version, ct);
            var variant = product.AddVariant(
                request.Variant.Sku,
                request.Variant.Price,
                request.Variant.CompareAtPrice,
                ProductPersistenceHelper.ResolveSelection(product, request.Variant.Options),
                maxVariants);
            product.EditVariant(
                variant.Id,
                request.Variant.Price,
                request.Variant.CompareAtPrice,
                request.Variant.IsActive,
                request.Variant.ImageMediaId);
            if (product.FulfillmentKind == ProductFulfillmentKind.Physical)
            {
                var location = await ProductPersistenceHelper.MainLocationAsync(db, ct);
                db.InventoryItems.Add(new InventoryItem(variant.Id, location.Id));
            }

            product.Validate(type);
            return true;
        }, ct);

        return await ProductDetailReader.ReadProductAsync(db, actor, users, request.ProductId, false, ct);
    }
}

