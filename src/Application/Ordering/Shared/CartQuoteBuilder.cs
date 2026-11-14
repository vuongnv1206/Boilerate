using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.DigitalInventory;
using Boilerate.Domain.Ordering;
using Microsoft.EntityFrameworkCore;
using CartEntity = Boilerate.Domain.Ordering.Cart;

namespace Boilerate.Application.Ordering.Shared;

internal static class CartQuoteBuilder
{
    internal static IQueryable<CartEntity> Carts(IApplicationDbContext db) => db.Carts.Include(c => c.Items);

    internal static async Task<(Discount? Discount, decimal Amount, string? Error)> DiscountQuoteAsync(
        IApplicationDbContext db,
        DateTime nowUtc,
        string? code,
        decimal subtotal,
        CancellationToken ct)
    {
        if (code is null)
        {
            return (null, 0, null);
        }

        var discount = await db.Set<Discount>().AsNoTracking().SingleOrDefaultAsync(d => d.Code == code, ct);
        if (discount is null)
        {
            return (null, 0, Discount.NotApplicable);
        }

        int used = await db.Orders.CountAsync(o => o.DiscountId == discount.Id && o.DiscountUsageHeld, ct);
        var quote = discount.Quote(subtotal, nowUtc, used);
        return (discount, quote.Amount, quote.Error);
    }

    internal static async Task<CartDto> GetCartForAsync(
        IApplicationDbContext db,
        OrderingSettings settings,
        DateTime nowUtc,
        Guid customer,
        CancellationToken ct)
    {
        var cart = await Carts(db).AsNoTracking().SingleOrDefaultAsync(c => c.CustomerId == customer, ct);
        if (cart is null)
        {
            return new CartDto(Guid.Empty, null, [], 0);
        }

        return await MapCartAsync(db, settings, nowUtc, cart, ct);
    }

    internal static async Task<CartDto> MapCartAsync(
        IApplicationDbContext db,
        OrderingSettings settings,
        DateTime nowUtc,
        CartEntity cart,
        CancellationToken ct)
    {
        var ids = cart.Items.Select(i => i.VariantId).ToArray();
        var products = await db.Products.AsNoTracking()
            .Where(p => p.Variants.Any(v => ids.Contains(v.Id)))
            .Include(p => p.Variants)
            .ToListAsync(ct);
        var stock = await db.InventoryItems.AsNoTracking().Where(i => ids.Contains(i.VariantId)).ToListAsync(ct);
        var counts = await db.DigitalStockItems.AsNoTracking()
            .Where(i => ids.Contains(i.VariantId) && i.State == DigitalStockState.Available)
            .GroupBy(i => i.VariantId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Id, g => g.Count, ct);
        var lines = cart.Items.OrderBy(i => i.VariantId).Select(line =>
        {
            var product = products.Single(p => p.Variants.Any(v => v.Id == line.VariantId));
            var variant = product.GetVariant(line.VariantId);
            bool available = !product.IsDeleted &&
                !variant.IsRetired &&
                product.Status == ProductStatus.Published &&
                variant.IsActive &&
                variant.Price.HasValue &&
                product.FulfillmentKind switch
                {
                    ProductFulfillmentKind.Physical => !variant.TrackInventory || stock.Any(i => i.VariantId == variant.Id && i.OnHand - i.Reserved >= line.Quantity),
                    ProductFulfillmentKind.DigitalUnique => counts.GetValueOrDefault(variant.Id) >= line.Quantity,
                    ProductFulfillmentKind.DigitalShared => counts.GetValueOrDefault(variant.Id) > 0,
                    ProductFulfillmentKind.ManualExtension => true,
                    _ => false,
                };
            return new CartLineDto(variant.Id, line.Quantity, product.Name, variant.Sku, variant.Price, available, product.FulfillmentKind);
        }).ToList();
        decimal subtotal = lines.Sum(i => (i.UnitPrice ?? 0) * i.Quantity);
        decimal shipping = products.Any(p => p.FulfillmentKind == ProductFulfillmentKind.Physical) ? settings.FlatShippingAmount : 0;
        CatalogRules.Money(shipping, null);
        var discount = await DiscountQuoteAsync(db, nowUtc, cart.DiscountCode, subtotal, ct);
        return new CartDto(cart.Id, cart.Version, lines, subtotal, shipping, cart.DiscountCode, discount.Amount, discount.Error);
    }

    internal static async Task<CartDto> SaveCartForAsync(
        IApplicationDbContext db,
        ICommerceUnitOfWork work,
        OrderingSettings settings,
        DateTime nowUtc,
        Guid customer,
        SaveCart r,
        CancellationToken ct)
    {
        if (r.Items is null || r.Items.Count > 50 || r.Items.Any(i => i is null))
        {
            throw new DomainException("Cart requires at most 50 lines.");
        }

        await work.ExecuteAsync(async () =>
        {
            await OrderingAuthHelper.AcquireOrderingGateAsync(db, ct);
            var cart = await Carts(db).SingleOrDefaultAsync(c => c.CustomerId == customer, ct);
            if (cart is null)
            {
                if (r.Version is not null)
                {
                    throw new ConflictException("Cart does not exist; reload before editing.");
                }

                cart = new CartEntity(customer);
                db.Carts.Add(cart);
            }
            else
            {
                CommerceConcurrency.CheckVersion(r.Version, cart.Version);
            }

            var ids = r.Items.Select(i => i.VariantId).Distinct().ToArray();
            if (await db.ProductVariants.CountAsync(
                    v => ids.Contains(v.Id) &&
                        v.IsActive &&
                        db.Products.Any(p => p.Id == v.ProductId && !p.IsDeleted && p.Status == ProductStatus.Published),
                    ct) != ids.Length)
            {
                throw new DomainException("Cart contains an unavailable product or SKU.");
            }

            db.Set<CartItem>().RemoveRange(cart.Items.Where(i => !ids.Contains(i.VariantId)).ToArray());
            cart.ReplaceItems(r.Items.Select(i => (i.VariantId, i.Quantity)));
            return true;
        }, ct);
        return await GetCartForAsync(db, settings, nowUtc, customer, ct);
    }

    internal static async Task<CartDto> SetDiscountForAsync(
        IApplicationDbContext db,
        ICommerceUnitOfWork work,
        OrderingSettings settings,
        DateTime nowUtc,
        Guid customer,
        SetCartDiscount request,
        CancellationToken ct)
    {
        await work.ExecuteAsync(async () =>
        {
            await OrderingAuthHelper.AcquireOrderingGateAsync(db, ct);
            var cart = await Carts(db).SingleOrDefaultAsync(c => c.CustomerId == customer, ct)
                ?? throw new ConflictException("Cart is empty.");
            CommerceConcurrency.CheckVersion(request.Version, cart.Version);
            if (cart.Items.Count == 0)
            {
                throw new ConflictException("Cart is empty.");
            }

            cart.SetDiscountCode(request.Code);
            return true;
        }, ct);
        return await GetCartForAsync(db, settings, nowUtc, customer, ct);
    }
}
