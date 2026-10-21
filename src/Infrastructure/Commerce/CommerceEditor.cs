using Boilerate.Application.Catalog;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Infrastructure.Commerce;

public sealed partial class CommerceService
{
    private static Guid ResolveDefault(Product product, string sku)
    {
        var normalized = CatalogRules.Code(sku).ToUpperInvariant();
        return product.Variants.SingleOrDefault(v => v.NormalizedSku == normalized)?.Id
            ?? throw new DomainException("Default SKU does not belong to this product.");
    }

    public async Task<ProductDto> SaveEditorAsync(SaveProductEditor r, CancellationToken ct)
    {
        if (r.Content is null || r.OptionEdits is null || r.VariantEdits is null || r.AddedVariants is null ||
            r.OptionEdits.Count > MaxOptions || r.VariantEdits.Count > MaxVariants || r.AddedVariants.Count > MaxVariants ||
            r.OptionEdits.Any(o => o is null || o.AddValues is null || o.Values is null || o.AddValues.Count > 200 || o.Values.Count > 200 ||
                o.AddValues.Any(v => v is null) || o.Values.Any(v => v is null)) ||
            r.VariantEdits.Any(v => v is null) || r.AddedVariants.Any(v => v is null) ||
            r.OptionEdits.Select(o => o.OptionId).Distinct().Count() != r.OptionEdits.Count ||
            r.VariantEdits.Select(v => v.VariantId).Distinct().Count() != r.VariantEdits.Count)
            throw new DomainException("Invalid editor changes.");

        await work.ExecuteAsync(async () =>
        {
            var (p, type) = await LockedProduct(r.Id, r.Version, ct);
            Version(r.ProductTypeVersion, type.Version);
            var originalIds = p.Variants.Select(v => v.Id).ToArray();
            var originalDefault = p.Variants.Single(v => v.IsDefault).Id;
            foreach (var edit in r.VariantEdits)
                p.GetVariant(edit.VariantId);
            if (r.AddedVariants.Any(v => v.Price.HasValue || v.CompareAtPrice.HasValue) || r.VariantEdits.Any(edit =>
                p.GetVariant(edit.VariantId).Price != edit.Price || p.GetVariant(edit.VariantId).CompareAtPrice != edit.CompareAtPrice))
                await PricePermission(ct);

            foreach (var edit in r.OptionEdits)
            {
                var option = p.Options.SingleOrDefault(o => o.Id == edit.OptionId) ?? throw new DomainException("Unknown option.");
                if (edit.Values.Select(v => v.Id).Distinct().Count() != edit.Values.Count)
                    throw new DomainException("Duplicate option value edit.");
                option.Rename(edit.Name, edit.SortOrder);
                foreach (var value in edit.Values)
                    (option.Values.SingleOrDefault(v => v.Id == value.Id) ?? throw new DomainException("Unknown option value.")).Rename(value.Label, value.SortOrder);
                foreach (var value in edit.AddValues)
                    option.AddValue(value.Code, value.Label);
            }

            var newVariants = r.AddedVariants.Select(input =>
                (Variant: p.AddVariant(input.Sku, input.Price, input.CompareAtPrice, ResolveSelection(p, input.Options), MaxVariants), Input: input)).ToList();
            var edits = r.VariantEdits.ToDictionary(v => v.VariantId);
            var additions = newVariants.ToDictionary(v => v.Variant.Id, v => v.Input);
            var states = p.Variants.Select(v => edits.TryGetValue(v.Id, out var edit)
                ? new VariantEditorState(v.Id, edit.Price, edit.CompareAtPrice, edit.IsActive, edit.ImageMediaId)
                : additions.TryGetValue(v.Id, out var added)
                    ? new VariantEditorState(v.Id, added.Price, added.CompareAtPrice, added.IsActive, added.ImageMediaId)
                    : new VariantEditorState(v.Id, v.Price, v.CompareAtPrice, v.IsActive, v.ImageMediaId)).ToArray();
            var defaultId = ResolveDefault(p, r.DefaultVariantSku);
            await ApplyContent(p, type, r.Content, ct, states, defaultId);
            p.Validate(type);

            if (defaultId != originalDefault)
            {
                // Release the filtered unique index before EF persists the final flags/new default row.
                await db.ProductVariants.Where(v => v.ProductId == p.Id && v.IsDefault)
                    .ExecuteUpdateAsync(s => s.SetProperty(v => v.IsDefault, false), ct);
                foreach (var variant in p.Variants.Where(v => originalIds.Contains(v.Id)))
                    db.Entry(variant).Property(v => v.IsDefault).IsModified = true;
            }
            if (p.FulfillmentKind == ProductFulfillmentKind.Physical && newVariants.Count > 0)
            {
                var location = await MainLocation(ct);
                foreach (var added in newVariants)
                    db.InventoryItems.Add(new InventoryItem(added.Variant.Id, location.Id));
            }
            return true;
        }, ct);
        return await GetAsync(r.Id, false, ct);
    }

    public Task<BulkProductStatusResult> BulkStatusAsync(BulkProductStatus r, CancellationToken ct)
    {
        if (r.Items is null || r.Items.Count is < 1 or > 100 || r.Items.Any(i => i is null || i.Id == Guid.Empty || string.IsNullOrWhiteSpace(i.Version)) ||
            r.Items.Select(i => i.Id).Distinct().Count() != r.Items.Count || r.Status is not (ProductStatus.Published or ProductStatus.Archived))
            throw new DomainException("Select 1–100 distinct products and Published or Archived.");
        return work.ExecuteAsync(async () =>
        {
            var ids = r.Items.Select(i => i.Id).ToArray();
            var owners = await db.Products.Where(p => ids.Contains(p.Id)).Select(p => new { p.Id, p.ProductTypeId }).ToListAsync(ct);
            if (owners.Count != ids.Length)
                throw new NotFoundException("A selected product no longer exists; reload the selection.");
            var types = new Dictionary<Guid, ProductType>();
            foreach (var typeId in owners.Select(p => p.ProductTypeId).Distinct().Order())
                types.Add(typeId, await LockedType(typeId, ct));
            var versions = r.Items.ToDictionary(i => i.Id, i => i.Version);
            var products = new Dictionary<Guid, Product>();
            foreach (var id in ids.Order())
            {
                await db.Products.Where(p => p.Id == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.Version, p => p.Version), ct);
                var p = await ProductGraph().SingleAsync(p => p.Id == id, ct);
                if (versions[id] != p.Version)
                    throw new ConflictException($"Product {id} changed; reload the selection.");
                products.Add(id, p);
            }
            foreach (var p in products.Values)
            {
                try { p.ChangeStatus(r.Status, types[p.ProductTypeId]); p.Validate(types[p.ProductTypeId]); }
                catch (DomainException e) { throw new DomainException($"Product {p.Id}: {e.Message}"); }
            }
            return new BulkProductStatusResult(r.Items.Select(i => new ProductStatusResult(i.Id, products[i.Id].Status, products[i.Id].Version)).ToList());
        }, ct);
    }
}
