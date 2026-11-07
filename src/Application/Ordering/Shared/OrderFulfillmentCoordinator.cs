using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Auditing;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.DigitalInventory;
using Boilerate.Domain.Inventory;
using Boilerate.Domain.Messaging;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Renewals;
using Boilerate.Domain.Shipping;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Ordering.Shared;

internal static class OrderFulfillmentCoordinator
{
    internal static readonly JsonSerializerOptions UnsafeJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    internal static string RenewalHash(object value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, UnsafeJsonOptions))));

    internal static void RecordRenewalOperation(
        IApplicationDbContext db,
        RenewalRequest request,
        Guid operationId,
        Guid actorId,
        string action,
        string hash,
        DateTime nowUtc) =>
        db.RenewalOperations.Add(new RenewalOperation(request.Id, operationId, actorId, action, hash, request.Status, nowUtc));

    internal static async Task EnsureRenewalRequestsAsync(
        IApplicationDbContext db,
        Order order,
        Guid actorId,
        DateTime nowUtc,
        CancellationToken ct)
    {
        if (order.PaymentStatus != OrderPaymentStatus.Paid || order.Status is OrderStatus.Cancelled or OrderStatus.Closed)
        {
            return;
        }

        var existing = await db.RenewalRequests
            .Where(r => r.OrderId == order.Id)
            .Select(r => r.OrderItemId)
            .ToListAsync(ct);
        foreach (var item in order.Items.Where(i => i.FulfillmentKind == ProductFulfillmentKind.ManualExtension && !existing.Contains(i.Id)))
        {
            var request = new RenewalRequest(order.Id, item.Id, item.VariantId);
            db.RenewalRequests.Add(request);
            RecordRenewalOperation(db, request, Guid.NewGuid(), actorId, "Created", RenewalHash(new { item.Id }), nowUtc);
        }
    }

    internal static async Task<bool> AllPhysicalDeliveredAsync(
        IApplicationDbContext db,
        Order order,
        CancellationToken ct)
    {
        var shipments = await db.Set<Shipment>()
            .Include(s => s.Items)
            .Where(s => s.OrderId == order.Id)
            .ToListAsync(ct);
        var delivered = shipments
            .Where(s => s.Status == ShipmentStatus.Delivered)
            .SelectMany(s => s.Items)
            .GroupBy(i => i.OrderItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        var physical = order.Items.Where(i => i.FulfillmentKind == ProductFulfillmentKind.Physical).ToArray();
        return physical.Length > 0 && physical.All(i => delivered.GetValueOrDefault(i.Id) == i.Quantity);
    }

    internal static async Task ReconcileOrderFulfillmentAsync(
        IApplicationDbContext db,
        Order order,
        CancellationToken ct)
    {
        if (order.Status == OrderStatus.Closed)
        {
            order.Touch();
            return;
        }

        bool complete = !order.Items.Any(i => i.FulfillmentKind == ProductFulfillmentKind.Physical) ||
            await AllPhysicalDeliveredAsync(db, order, ct);
        var renewals = await db.RenewalRequests.Where(r => r.OrderId == order.Id).ToListAsync(ct);
        if (order.Items.Any(i =>
                i.FulfillmentKind == ProductFulfillmentKind.ManualExtension &&
                !renewals.Any(r => r.OrderItemId == i.Id && r.Status == RenewalStatus.Completed)))
        {
            complete = false;
        }

        var delivered = await db.DigitalDeliveries
            .Where(d => d.OrderId == order.Id)
            .GroupBy(d => d.OrderItemId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Id, g => g.Count, ct);
        if (order.Items.Any(i =>
                (i.FulfillmentKind == ProductFulfillmentKind.DigitalUnique && delivered.GetValueOrDefault(i.Id) != i.Quantity) ||
                (i.FulfillmentKind == ProductFulfillmentKind.DigitalShared && delivered.GetValueOrDefault(i.Id) != 1)))
        {
            complete = false;
        }

        if (order.PhysicalDispatchStarted)
        {
            order.RegisterPhysicalShipment(complete);
        }
        else
        {
            order.CompleteFulfillment(complete);
        }
    }

    internal static IDataProtector DeliveryProtector(IDataProtectionProvider protection, Guid id) =>
        protection.CreateProtector("Boilerate.Ordering.DigitalDelivery.Content.v1", id.ToString("N"));

    internal static async Task FulfillPaidOrderAsync(
        IApplicationDbContext db,
        IDataProtectionProvider protection,
        Order order,
        Guid actorId,
        DateTime nowUtc,
        CancellationToken ct)
    {
        if (order.Status == OrderStatus.Closed)
        {
            throw new ConflictException("Closed orders cannot resume fulfillment.");
        }

        await OrderStockCoordinator.LockProductsAsync(db, order.Items.Select(i => i.ProductId), ct);
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

        var balances = await db.InventoryItems.Where(i => balanceIds.Contains(i.Id)).ToDictionaryAsync(i => i.VariantId, ct);
        var itemIds = order.Items.Select(i => i.Id).ToArray();
        var reservations = await db.PhysicalStockReservations
            .Where(r => itemIds.Contains(r.OrderItemId))
            .ToDictionaryAsync(r => r.OrderItemId, ct);
        var unitsByItem = new Dictionary<Guid, List<DigitalStockItem>>();
        bool sufficient = true;
        foreach (var item in order.Items.OrderBy(i => i.VariantId))
        {
            switch (item.FulfillmentKind)
            {
                case ProductFulfillmentKind.Physical:
                    if (!item.TrackInventory)
                    {
                        break;
                    }

                    var held = reservations.GetValueOrDefault(item.Id);
                    if (!balances.TryGetValue(item.VariantId, out var balance) ||
                        (held?.State == HoldState.Held
                            ? held.InventoryItemId != balance.Id || held.Quantity != item.Quantity || balance.Reserved < held.Quantity
                            : balance.Available < item.Quantity))
                    {
                        sufficient = false;
                    }

                    break;
                case ProductFulfillmentKind.DigitalUnique:
                    var units = await db.DigitalStockItems
                        .Where(u => u.VariantId == item.VariantId &&
                            (u.State == DigitalStockState.Available ||
                             (u.State == DigitalStockState.Reserved && u.ReservedForOrderItemId == item.Id)))
                        .OrderByDescending(u => u.ReservedForOrderItemId == item.Id)
                        .ThenBy(u => u.Id)
                        .Take(item.Quantity)
                        .ToListAsync(ct);
                    unitsByItem[item.Id] = units;
                    if (units.Count != item.Quantity)
                    {
                        sufficient = false;
                    }

                    break;
                case ProductFulfillmentKind.DigitalShared:
                    var shared = await db.DigitalStockItems
                        .SingleOrDefaultAsync(u => u.VariantId == item.VariantId && u.State == DigitalStockState.Available, ct);
                    unitsByItem[item.Id] = shared is null ? [] : [shared];
                    if (shared is null)
                    {
                        sufficient = false;
                    }

                    break;
                case ProductFulfillmentKind.ManualExtension:
                    break;
                default:
                    throw new DomainException("Unsupported fulfillment kind.");
            }
        }

        if (!sufficient)
        {
            order.MarkFulfillmentReview();
            return;
        }

        var preparedDeliveries = new List<DigitalDelivery>();
        try
        {
            foreach (var item in order.Items.Where(i => unitsByItem.ContainsKey(i.Id)))
            {
                int sequence = 0;
                foreach (var unit in unitsByItem[item.Id])
                {
                    Guid id = Guid.NewGuid();
                    string content = protection.CreateProtector("Boilerate.DigitalInventory.Content.v1", unit.Id.ToString("N"))
                        .Unprotect(unit.ProtectedContent);
                    preparedDeliveries.Add(new DigitalDelivery(
                        id,
                        order.Id,
                        item.Id,
                        item.VariantId,
                        unit.Id,
                        ++sequence,
                        item.FulfillmentKind,
                        DeliveryProtector(protection, id).Protect(content)));
                }
            }
        }
        catch (CryptographicException)
        {
            order.MarkFulfillmentReview();
            db.Trails.Add(new Trail
            {
                UserId = actorId,
                TableName = "Orders",
                PrimaryKey = order.Id.ToString(),
                Type = TrailType.Update,
                DateTime = nowUtc,
                NewValues = "{\"Action\":\"DeliveryDeferred\",\"Reason\":\"DigitalContentUnavailable\"}",
            });
            return;
        }

        foreach (var item in order.Items.OrderBy(i => i.VariantId))
        {
            if (item.FulfillmentKind == ProductFulfillmentKind.Physical && item.TrackInventory)
            {
                var balance = balances[item.VariantId];
                var held = reservations.GetValueOrDefault(item.Id);
                if (held?.State == HoldState.Held)
                {
                    continue;
                }

                balance.Reserve(item.Quantity);
                if (held is null)
                {
                    db.PhysicalStockReservations.Add(new PhysicalStockReservation(item.Id, item.VariantId, balance.Id, item.Quantity));
                }
                else
                {
                    held.Reacquire();
                }
            }
            else if (item.FulfillmentKind is ProductFulfillmentKind.DigitalUnique or ProductFulfillmentKind.DigitalShared)
            {
                foreach (var unit in unitsByItem[item.Id])
                {
                    if (item.FulfillmentKind == ProductFulfillmentKind.DigitalUnique)
                    {
                        if (unit.State == DigitalStockState.Available)
                        {
                            unit.Reserve(item.Id);
                        }

                        unit.Deliver(item.Id);
                    }
                }
            }
        }

        db.DigitalDeliveries.AddRange(preparedDeliveries);
        if (preparedDeliveries.Count > 0)
        {
            db.OutboxMessages.Add(OutboxMessage.DigitalDelivery(order.Id, actorId));
        }

        order.CompleteFulfillment(order.Items.All(i => i.FulfillmentKind is ProductFulfillmentKind.DigitalUnique or ProductFulfillmentKind.DigitalShared));
    }

    internal static async Task CloseAfterSalesAsync(
        IApplicationDbContext db,
        Order order,
        Guid actorId,
        DateTime nowUtc,
        CancellationToken ct)
    {
        order.CloseFulfillment();
        await OrderStockCoordinator.LockProductsAsync(db, order.Items.Select(i => i.ProductId), ct);
        var ids = order.Items.Select(i => i.Id).ToArray();
        var holds = await db.PhysicalStockReservations
            .Where(h => ids.Contains(h.OrderItemId) && h.State == HoldState.Held)
            .ToListAsync(ct);
        var balanceIds = holds.Select(h => h.InventoryItemId).Distinct().Order().ToArray();
        foreach (Guid id in balanceIds)
        {
            await db.InventoryItems
                .Where(i => i.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.Version, i => i.Version), ct);
        }

        var balances = await db.InventoryItems.Where(i => balanceIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        foreach (var hold in holds)
        {
            int remainder = hold.CloseRemainder();
            if (remainder > 0)
            {
                balances[hold.InventoryItemId].Release(remainder);
            }
        }

        var digital = await db.DigitalStockItems
            .Where(i => i.State == DigitalStockState.Reserved &&
                i.ReservedForOrderItemId.HasValue &&
                ids.Contains(i.ReservedForOrderItemId.Value))
            .ToListAsync(ct);
        foreach (var item in digital)
        {
            item.Release(item.ReservedForOrderItemId!.Value);
        }

        var renewals = await db.RenewalRequests.Where(r => r.OrderId == order.Id).ToListAsync(ct);
        foreach (var request in renewals)
        {
            if (request.ReleaseUnstarted())
            {
                RecordRenewalOperation(db, request, Guid.NewGuid(), actorId, "FulfillmentStopped", RenewalHash(new { OrderId = order.Id }), nowUtc);
            }
        }
    }

    internal static OrderDto MapOrder(Order order) =>
        new(
            order.Id,
            order.Number,
            order.Version,
            order.Status,
            order.PaymentMethod,
            order.Subtotal,
            order.ShippingAmount,
            order.Total,
            order.Currency,
            CommerceConcurrency.UtcTimestamp(order.ExpiresAt),
            order.RecipientName,
            order.Email,
            order.Phone,
            order.ShippingAddress,
            order.Items
                .OrderBy(i => i.Id)
                .Select(i => new OrderLineDto(
                    i.Id,
                    i.ProductId,
                    i.VariantId,
                    i.Name,
                    i.Sku,
                    i.OptionDescription,
                    i.FulfillmentKind,
                    i.UnitPrice,
                    i.Quantity,
                    i.DiscountAmount))
                .ToList(),
            order.PaymentStatus,
            order.FulfillmentStatus,
            CommerceConcurrency.UtcTimestamp(order.PaidAtUtc),
            CommerceConcurrency.UtcTimestamp(order.CreatedOn),
            order.PhysicalDispatchStarted,
            order.DiscountCode,
            order.DiscountAmount);
}

