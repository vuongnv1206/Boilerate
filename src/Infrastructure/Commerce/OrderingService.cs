using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Boilerate.Application.Catalog;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Ordering;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.DigitalInventory;
using Boilerate.Domain.Inventory;
using Boilerate.Domain.Messaging;
using Boilerate.Domain.Ordering;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.DataProtection;

namespace Boilerate.Infrastructure.Commerce;

public sealed partial class OrderingService(ApplicationDbContext db, CommerceUnitOfWork work, ICurrentUser actor,
    IUserService users, IOptions<OrderingSettings> settings, TimeProvider clock, IDataProtectionProvider protection) : IOrderingService, IPaymentService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private Guid Customer()
    {
        if (!actor.IsAuthenticated() || actor.GetUserId() == Guid.Empty)
            throw new UnauthorizedException("Sign in to use your cart and orders.");
        return actor.GetUserId();
    }
    private async Task AuthorizeAdmin(string action, CancellationToken ct)
    {
        var id = Customer();
        if (!await users.HasPermissionAsync(id.ToString(), $"Permissions.Order.{action}", ct))
            throw new ForbiddenException($"Order.{action} is required.");
    }
    private static void CheckVersion(string? expected, string current)
    {
        if (string.IsNullOrWhiteSpace(expected) || expected != current)
            throw new ConflictException("Version changed; reload before editing.");
    }
    private async Task Gate(CancellationToken ct)
    {
        if (await db.Set<CommerceLock>().Where(g => g.Name == "ordering")
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.Version, g => g.Version), ct) != 1)
            throw new ConflictException("Apply ordering migrations before checkout.");
    }
    private IQueryable<Cart> Carts() => db.Carts.Include(c => c.Items);
    private IQueryable<Order> Orders() => db.Orders.Include(o => o.Items);

    public async Task<CartDto> GetCartAsync(CancellationToken ct)
    {
        var customer = Customer();
        var cart = await Carts().AsNoTracking().SingleOrDefaultAsync(c => c.CustomerId == customer, ct);
        if (cart is null)
            return new(Guid.Empty, null, [], 0);
        return await MapCart(cart, ct);
    }

    public async Task<CartDto> SaveCartAsync(SaveCart r, CancellationToken ct)
    {
        var customer = Customer();
        if (r.Items is null || r.Items.Count > 50 || r.Items.Any(i => i is null))
            throw new DomainException("Cart requires at most 50 lines.");
        await work.ExecuteAsync(async () =>
        {
            await Gate(ct);
            var cart = await Carts().SingleOrDefaultAsync(c => c.CustomerId == customer, ct);
            if (cart is null)
            {
                if (r.Version is not null)
                    throw new ConflictException("Cart does not exist; reload before editing.");
                cart = new(customer);
                db.Carts.Add(cart);
            }
            else
                CheckVersion(r.Version, cart.Version);
            var ids = r.Items.Select(i => i.VariantId).Distinct().ToArray();
            if (await db.ProductVariants.CountAsync(v => ids.Contains(v.Id) && v.IsActive &&
                db.Products.Any(p => p.Id == v.ProductId && p.Status == ProductStatus.Published), ct) != ids.Length)
                throw new DomainException("Cart contains an unavailable product or SKU.");
            db.Set<CartItem>().RemoveRange(cart.Items.Where(i => !ids.Contains(i.VariantId)).ToArray());
            cart.ReplaceItems(r.Items.Select(i => (i.VariantId, i.Quantity)));
            return true;
        }, ct);
        return await GetCartAsync(ct);
    }

    private async Task<CartDto> MapCart(Cart cart, CancellationToken ct)
    {
        var ids = cart.Items.Select(i => i.VariantId).ToArray();
        var products = await db.Products.AsNoTracking().Where(p => p.Variants.Any(v => ids.Contains(v.Id)))
            .Include(p => p.Variants).ToListAsync(ct);
        var stock = await db.InventoryItems.AsNoTracking().Where(i => ids.Contains(i.VariantId)).ToListAsync(ct);
        var counts = await db.DigitalStockItems.AsNoTracking().Where(i => ids.Contains(i.VariantId) && i.State == DigitalStockState.Available)
            .GroupBy(i => i.VariantId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Id, g => g.Count, ct);
        var lines = cart.Items.OrderBy(i => i.VariantId).Select(line =>
        {
            var product = products.Single(p => p.Variants.Any(v => v.Id == line.VariantId));
            var variant = product.GetVariant(line.VariantId);
            var available = product.Status == ProductStatus.Published && variant.IsActive && variant.Price.HasValue &&
                product.FulfillmentKind switch
                {
                    ProductFulfillmentKind.Physical => stock.Any(i => i.VariantId == variant.Id && i.OnHand - i.Reserved >= line.Quantity),
                    ProductFulfillmentKind.DigitalUnique => counts.GetValueOrDefault(variant.Id) >= line.Quantity,
                    ProductFulfillmentKind.DigitalShared => counts.GetValueOrDefault(variant.Id) > 0,
                    ProductFulfillmentKind.ManualExtension => true,
                    _ => false
                };
            return new CartLineDto(variant.Id, line.Quantity, product.Name, variant.Sku, variant.Price, available);
        }).ToList();
        var subtotal = lines.Sum(i => (i.UnitPrice ?? 0) * i.Quantity);
        var shipping = products.Any(p => p.FulfillmentKind == ProductFulfillmentKind.Physical) ? settings.Value.FlatShippingAmount : 0;
        CatalogRules.Money(shipping, null);
        // Keep a repriced/oversized cart readable and editable; Order enforces persistable money bounds.
        return new(cart.Id, cart.Version, lines, subtotal, shipping);
    }

    private static string RequestHash(Checkout r) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
    {
        r.CartVersion, r.PaymentMethod, RecipientName = r.RecipientName?.Trim(), Email = r.Email?.Trim(), Phone = r.Phone?.Trim(),
        ShippingAddress = string.IsNullOrWhiteSpace(r.ShippingAddress) ? null : r.ShippingAddress.Trim(),
        ExpectedTotal = r.ExpectedTotal.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
    }))));

    private async Task<Dictionary<Guid, Product>> LockProducts(IEnumerable<Guid> productIds, CancellationToken ct)
    {
        var ids = productIds.Distinct().Order().ToArray();
        var types = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).Select(p => p.ProductTypeId).Distinct().OrderBy(id => id).ToArrayAsync(ct);
        foreach (var id in types)
            await db.ProductTypes.Where(t => t.Id == id).ExecuteUpdateAsync(s => s.SetProperty(t => t.Version, t => t.Version), ct);
        foreach (var id in ids)
            await db.Products.Where(p => p.Id == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.Version, p => p.Version), ct);
        return await db.Products.Where(p => ids.Contains(p.Id)).Include(p => p.Variants).ThenInclude(v => v.Selections)
            .Include(p => p.Options).ThenInclude(o => o.Values).AsSplitQuery().ToDictionaryAsync(p => p.Id, ct);
    }

    public async Task<OrderDto> CheckoutAsync(Checkout r, CancellationToken ct)
    {
        var customer = Customer();
        if (r.OperationId == Guid.Empty || string.IsNullOrWhiteSpace(r.CartVersion))
            throw new DomainException("OperationId and CartVersion are required.");
        CatalogRules.Money(r.ExpectedTotal, null);
        var hash = RequestHash(r);
        await ExpireAsync(ct);
        return await work.ExecuteAsync(async () =>
        {
            await Gate(ct);
            var prior = await Orders().SingleOrDefaultAsync(o => o.CustomerId == customer && o.OperationId == r.OperationId, ct);
            if (prior is not null)
            {
                if (prior.RequestHash != hash)
                    throw new ConflictException("OperationId was already used with different checkout content.");
                return MapOrder(prior);
            }
            var cart = await Carts().SingleOrDefaultAsync(c => c.CustomerId == customer, ct)
                ?? throw new ConflictException("Cart is empty.");
            CheckVersion(r.CartVersion, cart.Version);
            if (cart.Items.Count == 0)
                throw new ConflictException("Cart is empty.");
            var variantIds = cart.Items.Select(i => i.VariantId).ToArray();
            var productIds = await db.ProductVariants.Where(v => variantIds.Contains(v.Id)).Select(v => v.ProductId).Distinct().ToArrayAsync(ct);
            var products = await LockProducts(productIds, ct);
            var items = new List<OrderItem>();
            foreach (var line in cart.Items.OrderBy(i => i.VariantId))
            {
                var product = products.Values.Single(p => p.Variants.Any(v => v.Id == line.VariantId));
                var variant = product.GetVariant(line.VariantId);
                if (product.Status != ProductStatus.Published || !variant.IsActive || !variant.Price.HasValue)
                    throw new ConflictException("A cart product is no longer available for purchase.");
                var selections = variant.Selections.OrderBy(s => s.OptionId).Select(s =>
                {
                    var option = product.Options.Single(o => o.Id == s.OptionId);
                    return option.Name + ": " + option.Values.Single(v => v.Id == s.OptionValueId).Label;
                });
                items.Add(new(product.Id, variant.Id, product.Name, variant.Sku, string.Join("; ", selections), product.FulfillmentKind, variant.Price.Value, line.Quantity));
            }
            var shipping = items.Any(i => i.FulfillmentKind == ProductFulfillmentKind.Physical) ? settings.Value.FlatShippingAmount : 0;
            CatalogRules.Money(shipping, null);
            var order = new Order(customer, cart.Id, r.OperationId, hash, r.PaymentMethod, r.RecipientName, r.Email, r.Phone,
                r.ShippingAddress, shipping, Now, settings.Value.ReservationMinutes, items);
            if (order.Total != r.ExpectedTotal)
                throw new ConflictException("Prices or shipping changed; confirm the current total before checkout.");
            db.Orders.Add(order);
            await Reserve(order, ct);
            db.Set<CartItem>().RemoveRange(cart.Items.ToArray());
            cart.ReplaceItems([]);
            return MapOrder(order);
        }, ct);
    }

    private async Task Reserve(Order order, CancellationToken ct)
    {
        var physicalIds = order.Items.Where(i => i.FulfillmentKind == ProductFulfillmentKind.Physical).Select(i => i.VariantId).ToArray();
        var balanceIds = await db.InventoryItems.Where(i => physicalIds.Contains(i.VariantId) &&
            db.Set<InventoryLocation>().Any(l => l.Id == i.LocationId && l.Code == "MAIN" && l.IsActive))
            .OrderBy(i => i.Id).Select(i => i.Id).ToArrayAsync(ct);
        foreach (var id in balanceIds)
            await db.InventoryItems.Where(i => i.Id == id).ExecuteUpdateAsync(s => s.SetProperty(i => i.Version, i => i.Version), ct);
        var balances = await db.InventoryItems.Where(i => balanceIds.Contains(i.Id)).ToListAsync(ct);
        foreach (var item in order.Items.OrderBy(i => i.VariantId))
        {
            switch (item.FulfillmentKind)
            {
                case ProductFulfillmentKind.Physical:
                    var balance = balances.SingleOrDefault(i => i.VariantId == item.VariantId);
                    if (balance is null || balance.OnHand - balance.Reserved < item.Quantity)
                        throw new ConflictException("Insufficient physical stock; no order was created.");
                    balance.Reserve(item.Quantity);
                    db.PhysicalStockReservations.Add(new(item.Id, item.VariantId, balance.Id, item.Quantity));
                    break;
                case ProductFulfillmentKind.DigitalUnique:
                    var units = await db.DigitalStockItems.Where(i => i.VariantId == item.VariantId && i.State == DigitalStockState.Available)
                        .OrderBy(i => i.Id).Take(item.Quantity).ToListAsync(ct);
                    if (units.Count != item.Quantity)
                        throw new ConflictException("Insufficient digital stock; no order was created.");
                    foreach (var unit in units)
                        unit.Reserve(item.Id);
                    break;
                case ProductFulfillmentKind.DigitalShared:
                    if (!await db.DigitalStockItems.AnyAsync(i => i.VariantId == item.VariantId && i.State == DigitalStockState.Available, ct))
                        throw new ConflictException("Shared content is unavailable; no order was created.");
                    break;
                case ProductFulfillmentKind.ManualExtension:
                    break;
                default:
                    throw new DomainException("Unsupported fulfillment kind.");
            }
        }
    }

    public async Task<OrderDto> GetOrderAsync(Guid id, bool admin, CancellationToken ct)
    {
        var customer = Customer();
        if (admin) await AuthorizeAdmin("View", ct);
        var order = await Orders().AsNoTracking().SingleOrDefaultAsync(o => o.Id == id && (admin || o.CustomerId == customer), ct)
            ?? throw new NotFoundException("Order not found.");
        return MapOrder(order);
    }
    public async Task<Page<OrderSummary>> OrdersAsync(int page, int size, bool admin, CancellationToken ct)
    {
        var customer = Customer();
        if (admin) await AuthorizeAdmin("View", ct);
        CommerceService.ValidatePage(page, size);
        var query = db.Orders.AsNoTracking().Where(o => admin || o.CustomerId == customer);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(o => o.CreatedOn).ThenBy(o => o.Id).Skip((page - 1) * size).Take(size)
            .Select(o => new OrderSummary(o.Id, o.Number, o.Status, o.PaymentMethod, o.Total, o.Currency, o.CreatedOn, o.ExpiresAt,
                o.PaymentStatus, o.FulfillmentStatus, o.PaidAtUtc)).ToListAsync(ct);
        return new(items, total, page, size);
    }
    public async Task<OrderDto> CancelAsync(Guid id, CancelOrder r, bool admin, CancellationToken ct)
    {
        var customer = Customer();
        if (admin) await AuthorizeAdmin("Cancel", ct);
        return await work.ExecuteAsync(async () =>
        {
            await Gate(ct);
            var order = await Orders().SingleOrDefaultAsync(o => o.Id == id && (admin || o.CustomerId == customer), ct)
                ?? throw new NotFoundException("Order not found.");
            CheckVersion(r.Version, order.Version);
            if (order.PaymentStatus == OrderPaymentStatus.Paid)
                throw new ConflictException("Paid orders require the refund/review workflow and cannot be cancelled here.");
            if (order.Status is OrderStatus.Cancelled or OrderStatus.Expired)
                return MapOrder(order);
            await Release(order, ct);
            order.Cancel();
            return MapOrder(order);
        }, ct);
    }
    private async Task Release(Order order, CancellationToken ct)
    {
        await LockProducts(order.Items.Select(i => i.ProductId), ct);
        var itemIds = order.Items.Select(i => i.Id).ToArray();
        var reservations = await db.PhysicalStockReservations.Where(r => itemIds.Contains(r.OrderItemId) && r.State == HoldState.Held).ToListAsync(ct);
        var ids = reservations.Select(r => r.InventoryItemId).Distinct().Order().ToArray();
        foreach (var id in ids)
            await db.InventoryItems.Where(i => i.Id == id).ExecuteUpdateAsync(s => s.SetProperty(i => i.Version, i => i.Version), ct);
        var balances = await db.InventoryItems.Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        foreach (var reservation in reservations)
        {
            balances[reservation.InventoryItemId].Release(reservation.Quantity);
            reservation.Release();
        }
        var units = await db.DigitalStockItems.Where(i => i.ReservedForOrderItemId.HasValue && itemIds.Contains(i.ReservedForOrderItemId.Value)).ToListAsync(ct);
        foreach (var unit in units)
            unit.Release(unit.ReservedForOrderItemId!.Value);
    }
    public Task<int> ExpireAsync(CancellationToken ct) => work.ExecuteAsync(async () =>
    {
        await Gate(ct);
        var now = Now;
        var expired = await Orders().Where(o => o.Status == OrderStatus.PendingPayment && o.ExpiresAt <= now)
            .OrderBy(o => o.ExpiresAt).ThenBy(o => o.Id).Take(settings.Value.ExpirationBatchSize).ToListAsync(ct);
        foreach (var order in expired)
        {
            await Release(order, ct);
            order.Expire(now);
        }
        return expired.Count;
    }, ct);

    private static OrderDto MapOrder(Order order) => new(order.Id, order.Number, order.Version, order.Status, order.PaymentMethod,
        order.Subtotal, order.ShippingAmount, order.Total, order.Currency, order.ExpiresAt, order.RecipientName, order.Email, order.Phone,
        order.ShippingAddress, order.Items.OrderBy(i => i.Id).Select(i => new OrderLineDto(i.Id, i.ProductId, i.VariantId, i.Name, i.Sku,
            i.OptionDescription, i.FulfillmentKind, i.UnitPrice, i.Quantity)).ToList(), order.PaymentStatus, order.FulfillmentStatus, order.PaidAtUtc);
}
