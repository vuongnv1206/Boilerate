using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.DigitalInventory;
using Boilerate.Domain.Inventory;
using Boilerate.Domain.Ordering;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Ordering.Shared;

internal static class OrderStockCoordinator
{
    internal static IQueryable<Order> Orders(IApplicationDbContext db) => db.Orders.Include(o => o.Items);

    internal static async Task<Dictionary<Guid, Product>> LockProductsAsync(
        IApplicationDbContext db,
        IEnumerable<Guid> productIds,
        CancellationToken ct)
    {
        var ids = productIds.Distinct().Order().ToArray();
        var types = await db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => p.ProductTypeId)
            .Distinct()
            .OrderBy(id => id)
            .ToArrayAsync(ct);
        foreach (Guid id in types)
        {
            await db.ProductTypes
                .Where(t => t.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.Version, t => t.Version), ct);
        }

        foreach (Guid id in ids)
        {
            await db.Products
                .Where(p => p.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Version, p => p.Version), ct);
        }

        return await db.Products
            .Where(p => ids.Contains(p.Id))
            .Include(p => p.Variants)
            .ThenInclude(v => v.Selections)
            .Include(p => p.Options)
            .ThenInclude(o => o.Values)
            .AsSplitQuery()
            .ToDictionaryAsync(p => p.Id, ct);
    }

    internal static async Task ReserveStockAsync(IApplicationDbContext db, Order order, CancellationToken ct)
    {
        var physicalIds = order.Items
            .Where(i => i.FulfillmentKind == ProductFulfillmentKind.Physical)
            .Select(i => i.VariantId)
            .ToArray();
        var balanceIds = await db.InventoryItems
            .Where(i => physicalIds.Contains(i.VariantId) &&
                db.Set<InventoryLocation>().Any(l => l.Id == i.LocationId && l.Code == "MAIN" && l.IsActive))
            .OrderBy(i => i.Id)
            .Select(i => i.Id)
            .ToArrayAsync(ct);
        foreach (Guid id in balanceIds)
        {
            await db.InventoryItems
                .Where(i => i.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.Version, i => i.Version), ct);
        }

        var balances = await db.InventoryItems.Where(i => balanceIds.Contains(i.Id)).ToListAsync(ct);
        foreach (var item in order.Items.OrderBy(i => i.VariantId))
        {
            switch (item.FulfillmentKind)
            {
                case ProductFulfillmentKind.Physical:
                    if (!item.TrackInventory)
                    {
                        break;
                    }

                    var balance = balances.SingleOrDefault(i => i.VariantId == item.VariantId);
                    if (balance is null || balance.OnHand - balance.Reserved < item.Quantity)
                    {
                        throw new ConflictException("Insufficient physical stock; no order was created.");
                    }

                    balance.Reserve(item.Quantity);
                    db.PhysicalStockReservations.Add(new PhysicalStockReservation(item.Id, item.VariantId, balance.Id, item.Quantity));
                    break;
                case ProductFulfillmentKind.DigitalUnique:
                    var units = await db.DigitalStockItems
                        .Where(i => i.VariantId == item.VariantId && i.State == DigitalStockState.Available)
                        .OrderBy(i => i.Id)
                        .Take(item.Quantity)
                        .ToListAsync(ct);
                    if (units.Count != item.Quantity)
                    {
                        throw new ConflictException("Insufficient digital stock; no order was created.");
                    }

                    foreach (var unit in units)
                    {
                        unit.Reserve(item.Id);
                    }

                    break;
                case ProductFulfillmentKind.DigitalShared:
                    if (!await db.DigitalStockItems.AnyAsync(i => i.VariantId == item.VariantId && i.State == DigitalStockState.Available, ct))
                    {
                        throw new ConflictException("Shared content is unavailable; no order was created.");
                    }

                    break;
                case ProductFulfillmentKind.ManualExtension:
                    break;
                default:
                    throw new DomainException("Unsupported fulfillment kind.");
            }
        }
    }

    internal static async Task ReleaseStockAsync(IApplicationDbContext db, Order order, CancellationToken ct)
    {
        order.ReleaseDiscountUsage();
        await LockProductsAsync(db, order.Items.Select(i => i.ProductId), ct);
        var itemIds = order.Items.Select(i => i.Id).ToArray();
        var reservations = await db.PhysicalStockReservations
            .Where(r => itemIds.Contains(r.OrderItemId) && r.State == HoldState.Held)
            .ToListAsync(ct);
        var ids = reservations.Select(r => r.InventoryItemId).Distinct().Order().ToArray();
        foreach (Guid id in ids)
        {
            await db.InventoryItems
                .Where(i => i.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.Version, i => i.Version), ct);
        }

        var balances = await db.InventoryItems.Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        foreach (var reservation in reservations)
        {
            balances[reservation.InventoryItemId].Release(reservation.Quantity);
            reservation.Release();
        }

        var units = await db.DigitalStockItems
            .Where(i => i.ReservedForOrderItemId.HasValue && itemIds.Contains(i.ReservedForOrderItemId.Value))
            .ToListAsync(ct);
        foreach (var unit in units)
        {
            unit.Release(unit.ReservedForOrderItemId!.Value);
        }
    }

    internal static async Task<bool> HoldDiscountUsageAsync(IApplicationDbContext db, Order order, CancellationToken ct)
    {
        if (!order.DiscountId.HasValue || order.DiscountUsageHeld)
        {
            return true;
        }

        var discount = await db.Set<Discount>().AsNoTracking().SingleAsync(d => d.Id == order.DiscountId.Value, ct);
        int used = await db.Orders.CountAsync(o => o.DiscountId == discount.Id && o.DiscountUsageHeld, ct);
        if (discount.UsageLimit.HasValue && used >= discount.UsageLimit.Value)
        {
            return false;
        }

        order.HoldDiscountUsage();
        return true;
    }

    internal static Task<int> ExpirePendingOrdersAsync(
        IApplicationDbContext db,
        ICommerceUnitOfWork work,
        OrderingSettings settings,
        DateTime nowUtc,
        CancellationToken ct) =>
        work.ExecuteAsync(async () =>
        {
            await OrderingAuthHelper.AcquireOrderingGateAsync(db, ct);
            var expired = await Orders(db)
                .Where(o => o.Status == OrderStatus.PendingPayment && o.ExpiresAt <= nowUtc)
                .OrderBy(o => o.ExpiresAt)
                .ThenBy(o => o.Id)
                .Take(settings.ExpirationBatchSize)
                .ToListAsync(ct);
            foreach (var order in expired)
            {
                await ReleaseStockAsync(db, order, ct);
                order.Expire(nowUtc);
            }

            return expired.Count;
        }, ct);
}

