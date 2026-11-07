using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.Inventory;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Boilerate.Application.Catalog.Products;

public sealed record SaveProductEditorRequest(
    Guid Id,
    string Version,
    string ProductTypeVersion,
    ProductContent Content,
    List<OptionEditorInput> OptionEdits,
    List<VariantEditorInput> VariantEdits,
    List<VariantInput> AddedVariants,
    string DefaultVariantSku,
    List<OptionInput>? AddedOptions = null,
    CatalogConfiguration? Configuration = null) : IRequest<ProductDto>;

public sealed class SaveProductEditorRequestValidator : CustomValidator<SaveProductEditorRequest>
{
    public SaveProductEditorRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Version).NotEmpty();
        RuleFor(x => x.ProductTypeVersion).NotEmpty();
        RuleFor(x => x.Content).NotNull().SetValidator(new ProductContentValidator());
        RuleFor(x => x.DefaultVariantSku).MaximumLength(100);
        RuleFor(x => x.OptionEdits).NotNull().Must(x => x is null || x.Count <= 10);
        RuleFor(x => x.AddedOptions).Must(x => x is null || x.Count <= 10);
        RuleForEach(x => x.AddedOptions!).NotNull().SetValidator(new OptionInputValidator()).When(x => x.AddedOptions is not null);
        RuleForEach(x => x.OptionEdits).NotNull().ChildRules(option =>
        {
            option.RuleFor(x => x.OptionId).NotEmpty();
            option.RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            option.RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
            option.RuleFor(x => x.AddValues).NotNull().Must(x => x is null || x.Count <= 200);
            option.RuleForEach(x => x.AddValues).NotNull();
            option.RuleFor(x => x.Values).NotNull().Must(x => x is null || x.Count <= 200);
            option.RuleForEach(x => x.Values).NotNull();
        });
        RuleFor(x => x.VariantEdits).NotNull().Must(x => x is null || x.Count <= 1000);
        RuleForEach(x => x.VariantEdits).NotNull().ChildRules(variant => variant.RuleFor(x => x.VariantId).NotEmpty());
        RuleFor(x => x.AddedVariants).NotNull().Must(x => x is null || x.Count <= 1000);
        RuleForEach(x => x.AddedVariants).NotNull().SetValidator(new VariantInputValidator());
    }
}

public sealed class SaveProductEditorRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IUserService users,
    IOptions<CommerceSettings> settings) : IRequestHandler<SaveProductEditorRequest, ProductDto>
{
    public async Task<ProductDto> Handle(SaveProductEditorRequest request, CancellationToken ct)
    {
        if (request.Configuration is not null)
        {
            await work.ExecuteAsync(async () =>
            {
                var (product, type) = await ProductPersistenceHelper.LockProductAsync(db, request.Id, request.Version, ct);
                CommerceConcurrency.CheckVersion(request.ProductTypeVersion, type.Version);
                await ProductConfigurationApplier.ApplyConfigurationAsync(
                    db,
                    actor,
                    users,
                    product,
                    type,
                    request.Content,
                    request.Configuration,
                    false,
                    ct);
                return true;
            }, ct);
            return await ProductDetailReader.ReadProductAsync(db, actor, users, request.Id, false, ct);
        }

        int maxOptions = settings.Value.MaximumOptions;
        int maxVariants = settings.Value.MaximumVariants;
        if (request.Content is null ||
            request.OptionEdits is null ||
            request.VariantEdits is null ||
            request.AddedVariants is null ||
            request.OptionEdits.Count > maxOptions ||
            request.AddedOptions?.Count > maxOptions ||
            request.VariantEdits.Count > maxVariants ||
            request.AddedVariants.Count > maxVariants ||
            request.OptionEdits.Any(o =>
                o is null ||
                o.AddValues is null ||
                o.Values is null ||
                o.AddValues.Count > 200 ||
                o.Values.Count > 200 ||
                o.AddValues.Any(v => v is null) ||
                o.Values.Any(v => v is null)) ||
            request.VariantEdits.Any(v => v is null) ||
            request.AddedVariants.Any(v => v is null) ||
            request.OptionEdits.Select(o => o.OptionId).Distinct().Count() != request.OptionEdits.Count ||
            request.VariantEdits.Select(v => v.VariantId).Distinct().Count() != request.VariantEdits.Count)
        {
            throw new DomainException("Invalid editor changes.");
        }

        await work.ExecuteAsync(async () =>
        {
            var (product, type) = await ProductPersistenceHelper.LockProductAsync(db, request.Id, request.Version, ct);
            CommerceConcurrency.CheckVersion(request.ProductTypeVersion, type.Version);
            var originalIds = product.Variants.Select(v => v.Id).ToArray();
            Guid originalDefault = product.Variants.Single(v => v.IsDefault).Id;
            foreach (var edit in request.VariantEdits)
            {
                _ = product.GetVariant(edit.VariantId);
            }

            if (request.AddedVariants.Any(v => v.Price.HasValue || v.CompareAtPrice.HasValue) ||
                request.VariantEdits.Any(edit =>
                    product.GetVariant(edit.VariantId).Price != edit.Price ||
                    product.GetVariant(edit.VariantId).CompareAtPrice != edit.CompareAtPrice))
            {
                await ProductPersistenceHelper.RequirePricePermissionAsync(users, actor, ct);
            }

            foreach (var edit in request.OptionEdits)
            {
                var option = product.Options.SingleOrDefault(o => o.Id == edit.OptionId)
                    ?? throw new DomainException("Unknown option.");
                if (edit.Values.Select(v => v.Id).Distinct().Count() != edit.Values.Count)
                {
                    throw new DomainException("Duplicate option value edit.");
                }

                option.Rename(edit.Name, edit.SortOrder);
                foreach (var value in edit.Values)
                {
                    (option.Values.SingleOrDefault(v => v.Id == value.Id) ?? throw new DomainException("Unknown option value."))
                        .Rename(value.Label, value.SortOrder);
                }

                foreach (var value in edit.AddValues)
                {
                    option.AddValue(value.Code, value.Label);
                }
            }

            foreach (var input in request.AddedOptions ?? [])
            {
                var option = product.AddOption(input.Code, input.Name, maxOptions);
                foreach (var value in input.Values)
                {
                    option.AddValue(value.Code, value.Label);
                }

                Guid defaultValueId = option.Values.First().Id;
                foreach (Guid variantId in originalIds)
                {
                    product.AssignOptionValue(variantId, option.Id, defaultValueId);
                }
            }

            var newVariants = request.AddedVariants.Select(input =>
                (
                    Variant: product.AddVariant(
                        input.Sku,
                        input.Price,
                        input.CompareAtPrice,
                        ProductPersistenceHelper.ResolveSelection(product, input.Options),
                        maxVariants),
                    Input: input))
                .ToList();
            var edits = request.VariantEdits.ToDictionary(v => v.VariantId);
            var additions = newVariants.ToDictionary(v => v.Variant.Id, v => v.Input);
            var states = product.Variants.Select(v => edits.TryGetValue(v.Id, out var edit)
                ? new VariantEditorState(v.Id, edit.Price, edit.CompareAtPrice, edit.IsActive, edit.ImageMediaId)
                : additions.TryGetValue(v.Id, out var added)
                    ? new VariantEditorState(v.Id, added.Price, added.CompareAtPrice, added.IsActive, added.ImageMediaId)
                    : new VariantEditorState(v.Id, v.Price, v.CompareAtPrice, v.IsActive, v.ImageMediaId))
                .ToArray();
            Guid defaultId = ProductPersistenceHelper.ResolveDefault(product, request.DefaultVariantSku);
            await ProductPersistenceHelper.ApplyContentAsync(db, actor, product, type, request.Content, ct, states, defaultId);
            product.Validate(type);

            if (defaultId != originalDefault)
            {
                // Release the filtered unique index before EF persists the final flags/new default row.
                await db.ProductVariants
                    .Where(v => v.ProductId == product.Id && v.IsDefault)
                    .ExecuteUpdateAsync(s => s.SetProperty(v => v.IsDefault, false), ct);
                foreach (var variant in product.Variants.Where(v => originalIds.Contains(v.Id)))
                {
                    db.Entry(variant).Property(v => v.IsDefault).IsModified = true;
                }
            }

            if (product.FulfillmentKind == ProductFulfillmentKind.Physical && newVariants.Count > 0)
            {
                var location = await ProductPersistenceHelper.MainLocationAsync(db, ct);
                foreach (var added in newVariants)
                {
                    db.InventoryItems.Add(new InventoryItem(added.Variant.Id, location.Id));
                }
            }

            return true;
        }, ct);

        return await ProductDetailReader.ReadProductAsync(db, actor, users, request.Id, false, ct);
    }
}

