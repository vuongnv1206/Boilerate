using System.Diagnostics;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Identity.Users;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.DigitalInventory;
using Boilerate.Domain.Inventory;
using Boilerate.Domain.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Catalog.Products;

internal static class ProductPersistenceHelper
{
    internal static IQueryable<Product> ProductGraph(IApplicationDbContext db) =>
        db.Products
            .Where(p => !p.IsDeleted)
            .Include(x => x.Variants)
            .ThenInclude(x => x.Selections)
            .Include(x => x.Variants)
            .ThenInclude(x => x.Images)
            .Include(x => x.Options)
            .ThenInclude(x => x.Values)
            .Include(x => x.Categories)
            .Include(x => x.FieldValues)
            .Include(x => x.Media)
            .AsSplitQuery();

    internal static IQueryable<ProductVariant> StockedVariants(
        IApplicationDbContext db,
        IQueryable<ProductVariant> variants) =>
        variants.Where(v =>
            db.Products.Any(p => p.Id == v.ProductId &&
                ((p.FulfillmentKind == ProductFulfillmentKind.Physical &&
                    (!v.TrackInventory || db.InventoryItems.Any(i => i.VariantId == v.Id && i.OnHand > i.Reserved))) ||
                 ((p.FulfillmentKind == ProductFulfillmentKind.DigitalUnique || p.FulfillmentKind == ProductFulfillmentKind.DigitalShared) &&
                    db.DigitalStockItems.Any(i => i.VariantId == v.Id && i.State == DigitalStockState.Available)) ||
                 p.FulfillmentKind == ProductFulfillmentKind.ManualExtension)));

    internal static async Task RequirePricePermissionAsync(
        IUserService users,
        ICurrentUser actor,
        CancellationToken ct)
    {
        if (!await users.HasPermissionAsync(actor.GetUserId().ToString(), "Permissions.Price.Update", ct))
        {
            throw new ForbiddenException("Updating SKU prices requires Price.Update.");
        }
    }

    internal static async Task<(Product Product, ProductType Type)> LockProductAsync(
        IApplicationDbContext db,
        Guid id,
        string version,
        CancellationToken ct)
    {
        var typeId = await db.Products
            .Where(x => x.Id == id)
            .Select(x => (Guid?)x.ProductTypeId)
            .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Product not found.");
        var type = await CommerceConcurrency.LockProductTypeAsync(db, typeId, ct); // Consistent lock order: type -> product -> media.
        await db.Products
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Version, x => x.Version), ct);
        var product = await ProductGraph(db).SingleAsync(x => x.Id == id, ct);
        CommerceConcurrency.CheckVersion(version, product.Version);
        return (product, type);
    }

    internal static async Task<InventoryLocation> MainLocationAsync(IApplicationDbContext db, CancellationToken ct) =>
        await db.Set<InventoryLocation>().SingleOrDefaultAsync(l => l.Code == "MAIN" && l.IsActive, ct)
        ?? throw new ConflictException("Initialize commerce before creating SKUs.");

    internal static Dictionary<Guid, Guid> ResolveSelection(Product product, Dictionary<string, string>? selections)
    {
        if (selections is null || selections.Count != product.Options.Count(o => !o.IsRetired))
        {
            throw new DomainException("Invalid SKU selection.");
        }

        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in selections)
        {
            if (!normalized.TryAdd(pair.Key, pair.Value))
            {
                throw new DomainException("Duplicate option selection.");
            }
        }

        return product.Options
            .Where(o => !o.IsRetired)
            .ToDictionary(
                o => o.Id,
                o =>
                {
                    if (!normalized.TryGetValue(o.Code, out string? code))
                    {
                        throw new DomainException("An option selection is missing.");
                    }

                    return o.Values
                        .SingleOrDefault(v => string.Equals(v.Code, code, StringComparison.OrdinalIgnoreCase))?.Id
                        ?? throw new DomainException("Unknown option value.");
                });
    }

    internal static Guid ResolveDefault(Product product, string sku)
    {
        string normalized = (sku ?? string.Empty).Trim().ToUpperInvariant();
        var matches = product.Variants
            .Where(v => !v.IsRetired && v.NormalizedSku == normalized)
            .Take(2)
            .ToArray();
        return matches.Length == 1
            ? matches[0].Id
            : throw new DomainException("Identify the default variant by ID when SKU is empty or shared.");
    }

    internal static async Task ApplyContentAsync(
        IApplicationDbContext db,
        ICurrentUser actor,
        Product product,
        ProductType type,
        ProductContent content,
        CancellationToken ct,
        IReadOnlyCollection<VariantEditorState>? variantStates = null,
        Guid? defaultVariantId = null)
    {
        int maxMedia = variantStates is null ? 30 : Math.Max(30, variantStates.Count * 31);
        if (content.CategoryIds is null ||
            content.Fields is null ||
            content.Media is null ||
            content.Fields.Count > 100 ||
            content.Media.Count > maxMedia ||
            content.CategoryIds.Count > 100)
        {
            throw new DomainException("Invalid product content.");
        }

        var categoryIds = content.CategoryIds.Distinct().ToArray();
        if (await db.Categories.CountAsync(c => categoryIds.Contains(c.Id), ct) != categoryIds.Length)
        {
            throw new DomainException("Unknown category.");
        }

        var mediaIds = product.Media
            .Select(m => m.MediaAssetId)
            .Concat(content.Media.Select(m => m.MediaAssetId))
            .Distinct()
            .Order()
            .ToArray();
        foreach (Guid id in mediaIds)
        {
            if (await db.MediaAssets
                    .Where(m => m.Id == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.Version, m => m.Version), ct) != 1)
            {
                throw new DomainException("Unknown media asset.");
            }

            var asset = await db.MediaAssets.SingleAsync(m => m.Id == id, ct);
            if (content.Media.Any(m => m.MediaAssetId == id))
            {
                asset.Attach();
            }
        }

        foreach (var detached in product.Media.Where(m => content.Media.All(n => n.MediaAssetId != m.MediaAssetId)).ToArray())
        {
            db.Set<ProductMedia>().Remove(detached);
            db.OutboxMessages.Add(OutboxMessage.Cleanup(detached.MediaAssetId, actor.GetUserId(), Activity.Current?.Id));
        }

        db.Set<ProductCategory>().RemoveRange(product.Categories.Where(c => !categoryIds.Contains(c.CategoryId)).ToArray());
        db.Set<ProductFieldValue>().RemoveRange(product.FieldValues.Where(v => content.Fields.All(n => n.FieldDefinitionId != v.FieldDefinitionId)).ToArray());
        product.Edit(content.Name, content.Slug, content.Description);
        product.SetCategories(categoryIds);
        product.SetFields(type, content.Fields.Select(f => f.ToValue(product.Id, type.Id)));
        var gallery = content.Media.Select(m => new ProductMedia(product.Id, m.MediaAssetId, m.SortOrder, m.AltText)).ToArray();
        if (variantStates is null)
        {
            product.SetMedia(gallery);
        }
        else
        {
            product.ApplyEditorState(variantStates, gallery, defaultVariantId!.Value);
        }
    }
}

