using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Identity.Users;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.DigitalInventory;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Inventory.Digital;

public sealed record DigitalStockInput(string Reference, string Content);

public sealed record DigitalStockDto(
    Guid Id,
    Guid ProductId,
    Guid VariantId,
    ProductFulfillmentKind FulfillmentKind,
    string Reference,
    DigitalStockState State,
    string Version,
    string ProductVersion)
{
    public static DigitalStockDto FromEntity(DigitalStockItem item, string productVersion) =>
        new(item.Id, item.ProductId, item.VariantId, item.FulfillmentKind, item.Reference, item.State, item.Version, productVersion);
}

public sealed record DigitalSecretDto(Guid Id, string Content);

internal static class DigitalInventoryHelper
{
    internal static async Task RequirePermissionAsync(
        IUserService users,
        ICurrentUser actor,
        string action,
        CancellationToken ct)
    {
        if (!await users.HasPermissionAsync(actor.GetUserId().ToString(), $"Permissions.DigitalInventory.{action}", ct))
        {
            throw new ForbiddenException($"DigitalInventory.{action} is required.");
        }
    }

    internal static IDataProtector CreateProtector(IDataProtectionProvider protection, Guid id) =>
        protection.CreateProtector("Boilerate.DigitalInventory.Content.v1", id.ToString("N"));

    internal static async Task<Product> LockDigitalProductAsync(
        IApplicationDbContext db,
        Guid productId,
        string version,
        CancellationToken ct)
    {
        var typeId = await db.Products
            .Where(x => x.Id == productId && !x.IsDeleted)
            .Select(x => (Guid?)x.ProductTypeId)
            .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Product not found.");
        await CommerceConcurrency.LockProductTypeAsync(db, typeId, ct);
        await db.Products
            .Where(x => x.Id == productId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Version, x => x.Version), ct);
        var product = await db.Products
            .Where(p => !p.IsDeleted)
            .Include(x => x.Variants)
            .SingleAsync(x => x.Id == productId, ct);
        CommerceConcurrency.CheckVersion(version, product.Version);
        return product;
    }

    internal static void RequireDigitalVariant(Product product, Guid variantId, ProductFulfillmentKind expected)
    {
        _ = product.GetVariant(variantId);
        if (product.FulfillmentKind != expected)
        {
            throw new DomainException("Product fulfillment does not support this operation.");
        }
    }
}
