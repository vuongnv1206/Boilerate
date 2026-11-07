using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Identity.Users;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Catalog.Products;

internal static class ProductDetailReader
{
    internal static async Task<ProductDto> ReadProductAsync(
        IApplicationDbContext db,
        ICurrentUser actor,
        IUserService users,
        Guid id,
        bool publishedOnly,
        CancellationToken ct)
    {
        var product = await ProductPersistenceHelper.ProductGraph(db)
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == id && (!publishedOnly || p.Status == ProductStatus.Published), ct)
            ?? throw new NotFoundException("Product not found.");
        var ids = product.Variants.Select(v => v.Id).ToArray();
        bool canInventory = !publishedOnly &&
            await users.HasPermissionAsync(actor.GetUserId().ToString(), "Permissions.Inventory.View", ct);
        var inventory = canInventory
            ? await db.InventoryItems
                .AsNoTracking()
                .Where(i => ids.Contains(i.VariantId) &&
                    db.Set<InventoryLocation>().Any(l => l.Id == i.LocationId && l.Code == "MAIN"))
                .ToDictionaryAsync(i => i.VariantId, ct)
            : new Dictionary<Guid, InventoryItem>();
        var stocked = await ProductPersistenceHelper.StockedVariants(
                db,
                db.ProductVariants.Where(v => ids.Contains(v.Id) && v.IsActive))
            .Select(v => v.Id)
            .ToListAsync(ct);
        var fields = product.FieldValues.ToArray();
        var categories = product.Categories.Select(c => c.CategoryId).ToArray();
        if (publishedOnly)
        {
            var activeFields = await db.Set<ProductFieldDefinition>()
                .Where(f => f.ProductTypeId == product.ProductTypeId && f.IsActive)
                .Select(f => f.Id)
                .ToListAsync(ct);
            fields = fields.Where(f => activeFields.Contains(f.FieldDefinitionId)).ToArray();
            categories = await db.Categories
                .Where(c => categories.Contains(c.Id) && c.IsActive)
                .Select(c => c.Id)
                .ToArrayAsync(ct);
        }

        var definitions = await db.Set<ProductFieldDefinition>()
            .AsNoTracking()
            .Where(f => f.ProductTypeId == product.ProductTypeId)
            .Include(f => f.Choices)
            .ToListAsync(ct);

        return new ProductDto(
            product.Id,
            product.ProductTypeId,
            product.Name,
            product.Slug,
            product.Description,
            product.Status,
            product.Version,
            categories,
            product.Options
                .Where(o => !o.IsRetired)
                .OrderBy(o => o.SortOrder)
                .ThenBy(o => o.Id)
                .Select(o => new OptionDto(
                    o.Id,
                    o.Code,
                    o.Name,
                    o.SortOrder,
                    o.Values
                        .Where(v => !v.IsRetired)
                        .OrderBy(v => v.SortOrder)
                        .ThenBy(v => v.Id)
                        .Select(v => new OptionValueDto(v.Id, v.Code, v.Label, v.SortOrder))
                        .ToArray()))
                .ToArray(),
            product.Variants
                .Where(v => !v.IsRetired && (!publishedOnly || v.IsActive))
                .OrderByDescending(v => v.IsDefault)
                .ThenBy(v => v.Id)
                .Select(v => new VariantDto(
                    v.Id,
                    v.Sku,
                    v.CombinationKey,
                    v.Price,
                    v.CompareAtPrice,
                    v.Currency,
                    v.IsDefault,
                    v.IsActive,
                    v.ImageMediaId,
                    stocked.Contains(v.Id),
                    v.Selections.Select(s => new VariantSelectionDto(s.OptionId, s.OptionValueId)).ToArray(),
                    v.TrackInventory,
                    v.Weight,
                    v.Width,
                    v.Height,
                    v.Length,
                    v.Images.OrderBy(m => m.SortOrder).Select(m => new MediaInput(m.MediaAssetId, m.SortOrder, m.AltText)).ToArray(),
                    inventory.GetValueOrDefault(v.Id)?.OnHand,
                    inventory.GetValueOrDefault(v.Id)?.Version))
                .ToArray(),
            fields.Select(f =>
            {
                var definition = definitions.Single(d => d.Id == f.FieldDefinitionId);
                return new FieldValueDto(
                    f.FieldDefinitionId,
                    f.FieldType,
                    definition.Label,
                    f.TextValue,
                    f.NumberValue,
                    f.BooleanValue,
                    f.DateValue,
                    f.ChoiceId,
                    definition.Choices.SingleOrDefault(c => c.Id == f.ChoiceId)?.Label);
            }).ToArray(),
            product.Media
                .OrderBy(m => m.SortOrder)
                .ThenBy(m => m.MediaAssetId)
                .Select(m => new MediaInput(m.MediaAssetId, m.SortOrder, m.AltText))
                .ToArray(),
            product.FulfillmentKind,
            CommerceConcurrency.UtcTimestamp(product.CreatedOn),
            CommerceConcurrency.UtcTimestamp(product.LastModifiedOn ?? product.CreatedOn));
    }
}

