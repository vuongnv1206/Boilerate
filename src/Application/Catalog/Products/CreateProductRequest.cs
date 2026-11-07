using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.Inventory;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace Boilerate.Application.Catalog.Products;

public sealed record CreateProductRequest(
    Guid ProductTypeId,
    string ProductTypeVersion,
    ProductContent Content,
    List<OptionInput> Options,
    List<VariantInput> Variants,
    ProductFulfillmentKind FulfillmentKind = ProductFulfillmentKind.Physical,
    string? DefaultVariantSku = null,
    CatalogConfiguration? Configuration = null) : IRequest<ProductDto>;

public sealed class CreateProductRequestValidator : CustomValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.ProductTypeId).NotEmpty();
        RuleFor(x => x.FulfillmentKind).IsInEnum();
        RuleFor(x => x.ProductTypeVersion).NotEmpty();
        RuleFor(x => x.DefaultVariantSku).MaximumLength(100);
        RuleFor(x => x.Content).NotNull().SetValidator(new ProductContentValidator());
        RuleFor(x => x.Options).NotNull().Must(x => x is null || x.Count <= 10);
        RuleForEach(x => x.Options).NotNull().SetValidator(new OptionInputValidator());
        RuleFor(x => x.Variants).NotEmpty().Must(x => x is null || x.Count <= 1000).When(x => x.Configuration is null);
        RuleForEach(x => x.Variants).NotNull().SetValidator(new VariantInputValidator());
    }
}

public sealed class CreateProductRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IUserService users,
    IOptions<CommerceSettings> settings) : IRequestHandler<CreateProductRequest, ProductDto>
{
    public async Task<ProductDto> Handle(CreateProductRequest request, CancellationToken ct)
    {
        if (request.Configuration is not null)
        {
            Guid configuredId = await work.ExecuteAsync(async () =>
            {
                var type = await CommerceConcurrency.LockProductTypeAsync(db, request.ProductTypeId, ct);
                CommerceConcurrency.CheckVersion(request.ProductTypeVersion, type.Version);
                if (!type.IsActive)
                {
                    throw new ConflictException("Product type is inactive.");
                }

                var product = new Product(
                    type.Id,
                    request.Content.Name,
                    request.Content.Slug,
                    request.Content.Description,
                    request.FulfillmentKind);
                db.Products.Add(product);
                await ProductConfigurationApplier.ApplyConfigurationAsync(
                    db,
                    actor,
                    users,
                    product,
                    type,
                    request.Content,
                    request.Configuration,
                    true,
                    ct);
                return product.Id;
            }, ct);
            return await ProductDetailReader.ReadProductAsync(db, actor, users, configuredId, false, ct);
        }

        int maxOptions = settings.Value.MaximumOptions;
        int maxVariants = settings.Value.MaximumVariants;
        if (request.Variants is null ||
            request.Variants.Count < 1 ||
            request.Variants.Count > maxVariants ||
            request.Options is null)
        {
            throw new DomainException("Invalid variants/options.");
        }

        if (request.Variants.Any(v => v.Price.HasValue || v.CompareAtPrice.HasValue))
        {
            await ProductPersistenceHelper.RequirePricePermissionAsync(users, actor, ct);
        }

        Guid id = await work.ExecuteAsync(async () =>
        {
            var type = await CommerceConcurrency.LockProductTypeAsync(db, request.ProductTypeId, ct);
            CommerceConcurrency.CheckVersion(request.ProductTypeVersion, type.Version);
            if (!type.IsActive)
            {
                throw new ConflictException("Product type is inactive.");
            }

            var product = new Product(
                type.Id,
                request.Content.Name,
                request.Content.Slug,
                request.Content.Description,
                request.FulfillmentKind);
            foreach (var input in request.Options)
            {
                var option = product.AddOption(input.Code, input.Name, maxOptions);
                if (input.Values is null || input.Values.Count == 0)
                {
                    throw new DomainException("An option requires values.");
                }

                foreach (var value in input.Values)
                {
                    option.AddValue(value.Code, value.Label);
                }
            }

            foreach (var input in request.Variants)
            {
                product.AddVariant(
                    input.Sku,
                    input.Price,
                    input.CompareAtPrice,
                    ProductPersistenceHelper.ResolveSelection(product, input.Options),
                    maxVariants);
            }

            var states = request.Variants.Select((input, index) =>
            {
                var variant = product.Variants.ElementAt(index);
                return new VariantEditorState(variant.Id, input.Price, input.CompareAtPrice, input.IsActive, input.ImageMediaId);
            }).ToArray();
            Guid defaultId = request.DefaultVariantSku is null
                ? product.Variants.First().Id
                : ProductPersistenceHelper.ResolveDefault(product, request.DefaultVariantSku);
            await ProductPersistenceHelper.ApplyContentAsync(db, actor, product, type, request.Content, ct, states, defaultId);
            product.Validate(type);
            db.Products.Add(product);
            if (product.FulfillmentKind == ProductFulfillmentKind.Physical)
            {
                var location = await ProductPersistenceHelper.MainLocationAsync(db, ct);
                foreach (var variant in product.Variants)
                {
                    db.InventoryItems.Add(new InventoryItem(variant.Id, location.Id));
                }
            }

            return product.Id;
        }, ct);

        return await ProductDetailReader.ReadProductAsync(db, actor, users, id, false, ct);
    }
}

