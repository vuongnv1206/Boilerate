using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Ordering;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Shipping;
using Boilerate.Domain.Renewals;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Infrastructure.Commerce;

public sealed partial class OrderingService
{
    private async Task ShippingPermission(string action, CancellationToken ct)
    {
        var customer = Customer();
        if (!await users.HasPermissionAsync(customer.ToString(), $"Permissions.Shipping.{action}", ct))
            throw new ForbiddenException($"Shipping.{action} is required.");
    }

    private static string ShipmentHash(Guid orderId, string carrier, string tracking, IEnumerable<ShipmentLineInput> items) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            OrderId = orderId, Carrier = carrier, TrackingNumber = tracking,
            Items = items.OrderBy(i => i.OrderItemId).Select(i => new { i.OrderItemId, i.Quantity })
        }))));

    private IQueryable<Shipment> Shipments() => db.Set<Shipment>().Include(s => s.Items);

    public async Task<ShipmentDto> DispatchShipmentAsync(Guid orderId, DispatchShipment r, CancellationToken ct)
    {
        await ShippingPermission("Dispatch", ct);
        if (r.OperationId == Guid.Empty || r.Items is null || r.Items.Count is < 1 or > 50 ||
            r.Items.Any(i => i is null || i.OrderItemId == Guid.Empty || i.Quantity is < 1 or > 999) ||
            r.Items.Select(i => i.OrderItemId).Distinct().Count() != r.Items.Count)
            throw new DomainException("A shipment requires an operation ID and 1 to 50 distinct lines with quantities from 1 to 999.");
        var carrier = CatalogRules.Required(r.Carrier, 100, "Carrier");
        var tracking = CatalogRules.Required(r.TrackingNumber, 100, "TrackingNumber");
        var hash = ShipmentHash(orderId, carrier, tracking, r.Items);
        return await work.ExecuteAsync(async () =>
        {
            await Gate(ct);
            var prior = await Shipments().SingleOrDefaultAsync(s => s.OperationId == r.OperationId, ct);
            if (prior is not null)
            {
                if (prior.OrderId != orderId || prior.RequestHash != hash)
                    throw new ConflictException("OperationId was already used with different shipment content.");
                return MapShipment(prior, await db.Orders.SingleAsync(o => o.Id == orderId, ct));
            }
            var order = await Orders().SingleOrDefaultAsync(o => o.Id == orderId, ct)
                ?? throw new NotFoundException("Order not found.");
            CheckVersion(r.OrderVersion, order.Version);
            if (order.Status is OrderStatus.Cancelled or OrderStatus.Expired or OrderStatus.Closed || order.FulfillmentStatus == OrderFulfillmentStatus.NeedsReview ||
                (order.PaymentStatus != OrderPaymentStatus.Paid && order.PaymentMethod != OrderPaymentMethod.CashOnDelivery))
                throw new ConflictException("Only ready paid orders or physical COD orders can dispatch stock.");
            var physical = order.Items.Where(i => i.FulfillmentKind == ProductFulfillmentKind.Physical).ToDictionary(i => i.Id);
            if (r.Items.Any(i => !physical.ContainsKey(i.OrderItemId)))
                throw new ConflictException("Only physical items belonging to this order may be shipped.");
            await LockProducts(r.Items.Select(i => physical[i.OrderItemId].ProductId), ct);
            var orderItemIds = r.Items.Select(i => i.OrderItemId).ToArray();
            var previouslyShipped = await db.Set<ShipmentItem>().Where(i => orderItemIds.Contains(i.OrderItemId))
                .GroupBy(i => i.OrderItemId).Select(g => new { Id = g.Key, Quantity = g.Sum(i => i.Quantity) })
                .ToDictionaryAsync(i => i.Id, i => i.Quantity, ct);
            var reservations = await db.PhysicalStockReservations.Where(p => orderItemIds.Contains(p.OrderItemId))
                .ToDictionaryAsync(p => p.OrderItemId, ct);
            var balanceIds = reservations.Values.Select(p => p.InventoryItemId).Distinct().Order().ToArray();
            foreach (var id in balanceIds)
                await db.InventoryItems.Where(i => i.Id == id).ExecuteUpdateAsync(s => s.SetProperty(i => i.Version, i => i.Version), ct);
            var balances = await db.InventoryItems.Where(i => balanceIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
            foreach (var line in r.Items)
            {
                if (!physical[line.OrderItemId].TrackInventory)
                {
                    if (previouslyShipped.GetValueOrDefault(line.OrderItemId) + line.Quantity > physical[line.OrderItemId].Quantity)
                        throw new ConflictException("Shipment exceeds the order's remaining quantity.");
                    continue;
                }
                if (previouslyShipped.GetValueOrDefault(line.OrderItemId) + line.Quantity > physical[line.OrderItemId].Quantity ||
                    !reservations.TryGetValue(line.OrderItemId, out var reservation) || reservation.State != HoldState.Held ||
                    reservation.Remaining < line.Quantity || !balances.TryGetValue(reservation.InventoryItemId, out var balance) ||
                    balance.Reserved < line.Quantity || balance.OnHand < line.Quantity)
                    throw new ConflictException("Shipment exceeds the order's remaining reserved quantity.");
            }
            var lines = new List<ShipmentItem>();
            foreach (var line in r.Items.OrderBy(i => i.OrderItemId))
            {
                var item = physical[line.OrderItemId];
                if (!item.TrackInventory) { lines.Add(new(order.Id, item.Id, item.VariantId, null, line.Quantity)); continue; }
                var reservation = reservations[line.OrderItemId];
                var movement = balances[reservation.InventoryItemId].ConsumeReserved(Guid.NewGuid(), line.Quantity,
                    $"Dispatch order {order.Number}", actor.GetUserId());
                reservation.Consume(line.Quantity);
                db.StockMovements.Add(movement);
                lines.Add(new(order.Id, item.Id, item.VariantId, movement.Id, line.Quantity));
            }
            var shipment = new Shipment(order.Id, r.OperationId, hash, carrier, tracking, Now, lines);
            db.Set<Shipment>().Add(shipment);
            order.RegisterPhysicalShipment(false);
            return MapShipment(shipment, order);
        }, ct);
    }

    public async Task<ShipmentDto> DeliverShipmentAsync(Guid orderId, Guid shipmentId, DeliverShipment r, CancellationToken ct)
    {
        await ShippingPermission("Deliver", ct);
        if (r.OperationId == Guid.Empty)
            throw new DomainException("A delivery operation ID is required.");
        return await work.ExecuteAsync(async () =>
        {
            await Gate(ct);
            var prior = await Shipments().SingleOrDefaultAsync(s => s.DeliveryOperationId == r.OperationId, ct);
            if (prior is not null)
            {
                if (prior.Id != shipmentId || prior.OrderId != orderId)
                    throw new ConflictException("Delivery OperationId was already used for a different shipment.");
                return MapShipment(prior, await db.Orders.SingleAsync(o => o.Id == orderId, ct));
            }
            var order = await Orders().SingleOrDefaultAsync(o => o.Id == orderId, ct)
                ?? throw new NotFoundException("Order not found.");
            var shipment = await Shipments().SingleOrDefaultAsync(s => s.Id == shipmentId && s.OrderId == orderId, ct)
                ?? throw new NotFoundException("Shipment not found.");
            CheckVersion(r.OrderVersion, order.Version);
            CheckVersion(r.Version, shipment.Version);
            if (shipment.Status == ShipmentStatus.Delivered)
                throw new ConflictException("Shipment was already delivered; replay its original delivery operation.");
            shipment.MarkDelivered(r.OperationId, Now);
            await ReconcileOrderFulfillment(order, ct);
            return MapShipment(shipment, order);
        }, ct);
    }

    private async Task<bool> AllPhysicalDelivered(Order order, CancellationToken ct)
    {
        // Include tracked state: MarkDelivered may not yet have been flushed to the DB.
        var shipments = await Shipments().Where(s => s.OrderId == order.Id).ToListAsync(ct);
        var delivered = shipments.Where(s => s.Status == ShipmentStatus.Delivered).SelectMany(s => s.Items)
            .GroupBy(i => i.OrderItemId).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        var physical = order.Items.Where(i => i.FulfillmentKind == ProductFulfillmentKind.Physical).ToArray();
        return physical.Length > 0 && physical.All(i => delivered.GetValueOrDefault(i.Id) == i.Quantity);
    }

    private async Task ReconcileOrderFulfillment(Order order, CancellationToken ct)
    {
        if (order.Status == OrderStatus.Closed)
        {
            order.Touch(); // Record late delivery/verification facts without reopening fulfillment.
            return;
        }
        var complete = !order.Items.Any(i => i.FulfillmentKind == ProductFulfillmentKind.Physical) || await AllPhysicalDelivered(order, ct);
        // Materialize before evaluating status so verification in this transaction is visible.
        var renewals = await db.RenewalRequests.Where(r => r.OrderId == order.Id).ToListAsync(ct);
        if (order.Items.Any(i => i.FulfillmentKind == ProductFulfillmentKind.ManualExtension &&
            !renewals.Any(r => r.OrderItemId == i.Id && r.Status == RenewalStatus.Completed)))
            complete = false;
        var delivered = await db.DigitalDeliveries.Where(d => d.OrderId == order.Id)
            .GroupBy(d => d.OrderItemId).Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Id, g => g.Count, ct);
        if (order.Items.Any(i => i.FulfillmentKind == ProductFulfillmentKind.DigitalUnique && delivered.GetValueOrDefault(i.Id) != i.Quantity ||
                                i.FulfillmentKind == ProductFulfillmentKind.DigitalShared && delivered.GetValueOrDefault(i.Id) != 1))
            complete = false;
        if (order.PhysicalDispatchStarted) order.RegisterPhysicalShipment(complete);
        else order.CompleteFulfillment(complete);
    }

    public async Task<List<ShipmentDto>> ShipmentsAsync(Guid orderId, bool admin, CancellationToken ct)
    {
        var customer = Customer();
        if (admin) await ShippingPermission("View", ct);
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == orderId && (admin || o.CustomerId == customer), ct)
            ?? throw new NotFoundException("Order not found.");
        var shipments = await Shipments().AsNoTracking().Where(s => s.OrderId == orderId)
            .OrderBy(s => s.DispatchedAtUtc).ThenBy(s => s.Id).ToListAsync(ct);
        return shipments.Select(s => MapShipment(s, order)).ToList();
    }

    private static ShipmentDto MapShipment(Shipment shipment, Order order) => new(shipment.Id, order.Id, order.Version, shipment.Version,
        shipment.Carrier, shipment.TrackingNumber, shipment.Status, UtcTimestamp(shipment.DispatchedAtUtc), UtcTimestamp(shipment.DeliveredAtUtc),
        shipment.Items.OrderBy(i => i.OrderItemId).Select(i => new ShipmentLineDto(i.OrderItemId, i.Quantity)).ToList());
}
