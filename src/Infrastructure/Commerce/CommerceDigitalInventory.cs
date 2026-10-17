using Boilerate.Application.Catalog;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Domain.Auditing;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.DigitalInventory;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Infrastructure.Commerce;

public sealed partial class CommerceService
{
    private async Task DigitalPermission(string action, CancellationToken ct)
    {
        if (!await users.HasPermissionAsync(actor.GetUserId().ToString(), $"Permissions.DigitalInventory.{action}", ct))
            throw new ForbiddenException($"DigitalInventory.{action} is required.");
    }

    private IDataProtector DigitalProtector(Guid id) => protection.CreateProtector("Boilerate.DigitalInventory.Content.v1", id.ToString("N"));

    private static DigitalStockDto MapDigital(DigitalStockItem item, string productVersion) =>
        new(item.Id, item.ProductId, item.VariantId, item.FulfillmentKind, item.Reference, item.State, item.Version, productVersion);

    private static void RequireDigitalVariant(Product product, Guid variantId, ProductFulfillmentKind expected)
    {
        _ = product.GetVariant(variantId);
        if (product.FulfillmentKind != expected)
            throw new DomainException("Product fulfillment does not support this operation.");
    }

    public async Task<List<DigitalStockDto>> ImportDigitalAsync(ImportDigitalStock r, CancellationToken ct)
    {
        await DigitalPermission("Import", ct);
        if (r.Items is null || r.Items.Count is < 1 or > 100 || r.Items.Any(i => i is null))
            throw new DomainException("Import requires between 1 and 100 digital stock items.");
        var references = r.Items.Select(i => CatalogRules.Code(i.Reference).ToUpperInvariant()).ToArray();
        if (references.Distinct(StringComparer.Ordinal).Count() != references.Length)
            throw new ConflictException("Duplicate digital stock references in import.");
        foreach (var item in r.Items)
            DigitalStockItem.ValidateContent(item.Content);

        return await work.ExecuteAsync(async () =>
        {
            var (product, _) = await LockedProduct(r.ProductId, r.Version, ct);
            RequireDigitalVariant(product, r.VariantId, ProductFulfillmentKind.DigitalUnique);
            if (await db.DigitalStockItems.AnyAsync(i => i.VariantId == r.VariantId && references.Contains(i.Reference), ct))
                throw new ConflictException("A digital stock reference already exists for this SKU.");
            var imported = r.Items.Select((input, index) =>
            {
                var id = Guid.NewGuid();
                return new DigitalStockItem(id, product.Id, r.VariantId, product.FulfillmentKind,
                    references[index], DigitalProtector(id).Protect(input.Content));
            }).ToList();
            db.DigitalStockItems.AddRange(imported);
            product.Touch();
            return imported.Select(item => MapDigital(item, product.Version)).ToList();
        }, ct);
    }

    public async Task<DigitalStockDto> SaveSharedAsync(SaveSharedDigitalContent r, CancellationToken ct)
    {
        await DigitalPermission("Update", ct);
        DigitalStockItem.ValidateContent(r.Content);
        return await work.ExecuteAsync(async () =>
        {
            var (product, _) = await LockedProduct(r.ProductId, r.Version, ct);
            RequireDigitalVariant(product, r.VariantId, ProductFulfillmentKind.DigitalShared);
            var item = await db.DigitalStockItems.SingleOrDefaultAsync(i => i.VariantId == r.VariantId, ct);
            if (item is null)
            {
                var id = Guid.NewGuid();
                item = new(id, product.Id, r.VariantId, product.FulfillmentKind, "SHARED", DigitalProtector(id).Protect(r.Content));
                db.DigitalStockItems.Add(item);
            }
            else
                item.ReplaceSharedContent(DigitalProtector(item.Id).Protect(r.Content));
            item.SetAvailability(r.IsActive);
            product.Touch();
            return MapDigital(item, product.Version);
        }, ct);
    }

    public async Task<DigitalStockDto> SetDigitalStockAvailabilityAsync(SetDigitalStockAvailability r, CancellationToken ct)
    {
        await DigitalPermission("Update", ct);
        return await work.ExecuteAsync(async () =>
        {
            var (product, _) = await LockedProduct(r.ProductId, r.Version, ct);
            _ = product.GetVariant(r.VariantId);
            var item = await db.DigitalStockItems.SingleOrDefaultAsync(i => i.Id == r.StockItemId && i.ProductId == product.Id && i.VariantId == r.VariantId, ct)
                ?? throw new NotFoundException("Digital stock item not found.");
            item.SetAvailability(r.IsAvailable);
            product.Touch();
            return MapDigital(item, product.Version);
        }, ct);
    }

    public async Task<Page<DigitalStockDto>> DigitalStockAsync(Guid variantId, int page, int size, CancellationToken ct)
    {
        await DigitalPermission("View", ct);
        ValidatePage(page, size);
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Variants.Any(v => v.Id == variantId), ct)
            ?? throw new NotFoundException("SKU not found.");
        if (product.FulfillmentKind is not ProductFulfillmentKind.DigitalUnique and not ProductFulfillmentKind.DigitalShared)
            throw new DomainException("This SKU does not use digital stock.");
        var query = db.DigitalStockItems.AsNoTracking().Where(i => i.VariantId == variantId);
        int total = await query.CountAsync(ct);
        // Project metadata in SQL so ciphertext is never materialized for a list response.
        var rows = await query.OrderBy(i => i.Id).Skip((page - 1) * size).Take(size)
            .Select(i => new DigitalStockDto(i.Id, i.ProductId, i.VariantId, i.FulfillmentKind, i.Reference, i.State, i.Version, product.Version)).ToListAsync(ct);
        return new(rows, total, page, size);
    }

    public async Task<DigitalSecretDto> RevealDigitalStockAsync(Guid id, CancellationToken ct)
    {
        await DigitalPermission("Reveal", ct);
        return await work.ExecuteAsync(async () =>
        {
            var item = await db.DigitalStockItems.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id, ct)
                ?? throw new NotFoundException("Digital stock item not found.");
            var content = DigitalProtector(item.Id).Unprotect(item.ProtectedContent);
            db.Trails.Add(new Trail
            {
                UserId = actor.GetUserId(), TableName = "DigitalStockItems", PrimaryKey = item.Id.ToString(),
                Type = TrailType.Read, DateTime = DateTime.UtcNow, NewValues = "{\"Action\":\"Reveal\"}"
            });
            return new DigitalSecretDto(item.Id, content);
        }, ct);
    }
}
