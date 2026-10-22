using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Ordering;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.DigitalInventory;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Payments;
using Boilerate.Domain.Shipping;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Infrastructure.Commerce;

public sealed partial class OrderingService
{
    private async Task AfterSalesPermission(string function, string action, CancellationToken ct)
    {
        if (!await users.HasPermissionAsync(Customer().ToString(), $"Permissions.{function}.{action}", ct))
            throw new ForbiddenException($"{function}.{action} is required.");
    }

    private static string AfterSalesHash(object payload) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))));
    private static DateTime AfterSalesTime(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc) throw new DomainException("After-sales timestamps must be UTC.");
        return new(value.Ticks - value.Ticks % 10, DateTimeKind.Utc);
    }
    private static RefundDto MapRefund(Refund r) => new(r.Id, r.ReceiptId, r.Reference, r.Source, r.Amount, r.Currency, r.Reason, UtcTimestamp(r.RefundedAtUtc));
    private static PhysicalReturnDto MapReturn(PhysicalReturn r) => new(r.Id, r.Reference, r.Reason, UtcTimestamp(r.ReceivedAtUtc),
        r.CloseRemainingCod, r.Items.OrderBy(i => i.ShipmentId).ThenBy(i => i.OrderItemId)
            .Select(i => new ReturnLineDto(i.ShipmentId, i.OrderItemId, i.Quantity, i.RestockQuantity)).ToList());

    public async Task<AfterSalesDto> AfterSalesAsync(Guid orderId, bool admin, CancellationToken ct)
    {
        Customer();
        if (admin) await AfterSalesPermission("AfterSales", "View", ct);
        return await work.ExecuteAsync(async () =>
        {
            await Gate(ct); // Keep the order version and ledger totals from the same committed state.
            var order = await RenewalOrder(orderId, !admin, ct);
            var received = await db.PaymentReceipts.Where(p => p.OrderId == orderId).Select(p => (decimal?)p.Amount).SingleOrDefaultAsync(ct) ?? 0;
            var refunds = await db.Refunds.AsNoTracking().Where(r => r.OrderId == orderId).OrderBy(r => r.RefundedAtUtc).ThenBy(r => r.Id).ToListAsync(ct);
            var returns = await db.PhysicalReturns.AsNoTracking().Include(r => r.Items).Where(r => r.OrderId == orderId)
                .OrderBy(r => r.ReceivedAtUtc).ThenBy(r => r.Id).ToListAsync(ct);
            var refunded = refunds.Sum(r => r.Amount);
            return new AfterSalesDto(orderId, order.Version, order.Status == OrderStatus.Closed, received, refunded, received - refunded,
                refunds.Select(MapRefund).ToList(), returns.Select(MapReturn).ToList());
        }, ct);
    }

    public async Task<RefundResult> ConfirmRefundAsync(Guid orderId, ConfirmRefund input, CancellationToken ct)
    {
        await AfterSalesPermission("Refund", "Confirm", ct);
        if (input.OperationId == Guid.Empty) throw new DomainException("OperationId is required.");
        var reference = CatalogRules.Code(input.Reference);
        var normalized = reference.ToUpperInvariant();
        var reason = CatalogRules.Required(input.Reason, 1000, "Reason");
        var currency = CatalogRules.Required(input.Currency, 3, "Currency").ToUpperInvariant();
        var source = CatalogRules.Required(input.Source, 32, "Source");
        CatalogRules.Money(input.Amount, null);
        if (input.Amount <= 0 || currency != "VND" || source is not ("ManualBankTransfer" or "ManualCash"))
            throw new DomainException("Refunds require a positive whole VND amount and a supported manual source.");
        var refundedAt = AfterSalesTime(input.RefundedAtUtc);
        var actorId = Customer();
        var hash = AfterSalesHash(new { OrderId = orderId, ActorId = actorId, Reference = normalized, Reason = reason, Currency = currency,
            Source = source, RefundedAtUtc = refundedAt, Amount = input.Amount.ToString("0.##", CultureInfo.InvariantCulture) });
        return await work.ExecuteAsync(async () =>
        {
            await Gate(ct);
            var prior = await db.Refunds.SingleOrDefaultAsync(r => r.OperationId == input.OperationId, ct);
            if (prior is not null)
            {
                if (prior.OrderId != orderId || prior.ActorId != actorId || prior.RequestHash != hash)
                    throw new ConflictException("Refund OperationId was already used for another actor, order or content.");
                var current = await db.Orders.SingleAsync(o => o.Id == orderId, ct);
                return new RefundResult(MapRefund(prior), current.Version, current.Status == OrderStatus.Closed);
            }
            var order = await RenewalOrder(orderId, false, ct);
            CheckVersion(input.OrderVersion, order.Version);
            var receipt = await db.PaymentReceipts.SingleOrDefaultAsync(p => p.OrderId == orderId, ct)
                ?? throw new ConflictException("A confirmed receipt is required before recording a refund.");
            if (order.PaymentStatus != OrderPaymentStatus.Paid) throw new ConflictException("Only received money can be refunded.");
            if (await db.Refunds.AnyAsync(r => r.Source == source && r.NormalizedReference == normalized, ct))
                throw new ConflictException("This refund reference has already been recorded.");
            var total = await db.Refunds.Where(r => r.ReceiptId == receipt.Id).SumAsync(r => (decimal?)r.Amount, ct) ?? 0;
            var refund = new Refund(receipt.Id, orderId, input.OperationId, hash, reference, input.Amount, currency, refundedAt,
                UtcTimestamp(receipt.ReceivedAtUtc), Now, reason, source, actorId);
            if (currency != receipt.Currency || refund.Amount > receipt.Amount - total)
                throw new ConflictException("Refunds cannot exceed the receipt's remaining amount or change currency.");
            db.Refunds.Add(refund);
            if (total + refund.Amount == receipt.Amount) await CloseAfterSales(order, ct);
            order.Touch();
            return new RefundResult(MapRefund(refund), order.Version, order.Status == OrderStatus.Closed);
        }, ct);
    }

    private async Task CloseAfterSales(Order order, CancellationToken ct)
    {
        order.CloseFulfillment();
        await LockProducts(order.Items.Select(i => i.ProductId), ct);
        var ids = order.Items.Select(i => i.Id).ToArray();
        var holds = await db.PhysicalStockReservations.Where(h => ids.Contains(h.OrderItemId) && h.State == HoldState.Held).ToListAsync(ct);
        var balanceIds = holds.Select(h => h.InventoryItemId).Distinct().Order().ToArray();
        foreach (var id in balanceIds)
            await db.InventoryItems.Where(i => i.Id == id).ExecuteUpdateAsync(s => s.SetProperty(i => i.Version, i => i.Version), ct);
        var balances = await db.InventoryItems.Where(i => balanceIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        foreach (var hold in holds)
        {
            var remainder = hold.CloseRemainder();
            if (remainder > 0) balances[hold.InventoryItemId].Release(remainder);
        }
        // Delivered Unique retains its owner/content forever. Only unallocated reservations may be released.
        var digital = await db.DigitalStockItems.Where(i => i.State == DigitalStockState.Reserved &&
            i.ReservedForOrderItemId.HasValue && ids.Contains(i.ReservedForOrderItemId.Value)).ToListAsync(ct);
        foreach (var item in digital) item.Release(item.ReservedForOrderItemId!.Value);
        var renewals = await db.RenewalRequests.Where(r => r.OrderId == order.Id).ToListAsync(ct);
        foreach (var request in renewals)
            if (request.ReleaseUnstarted())
                RecordRenewal(request, Guid.NewGuid(), "FulfillmentStopped", RenewalHash(new { OrderId = order.Id }));
    }

    public async Task<ReturnResult> ReceiveReturnAsync(Guid orderId, ReceiveReturn input, CancellationToken ct)
    {
        await AfterSalesPermission("Return", "Receive", ct);
        if (input.OperationId == Guid.Empty || input.Items is null || input.Items.Count is < 1 or > 50 ||
            input.Items.Any(i => i is null || i.ShipmentId == Guid.Empty || i.OrderItemId == Guid.Empty || i.Quantity is < 1 or > 999 ||
                i.RestockQuantity < 0 || i.RestockQuantity > i.Quantity) ||
            input.Items.Select(i => (i.ShipmentId, i.OrderItemId)).Distinct().Count() != input.Items.Count)
            throw new DomainException("Returns require 1 to 50 distinct shipment lines with valid received/restock quantities.");
        if (input.Items.Any(i => i.RestockQuantity > 0)) await AfterSalesPermission("Inventory", "Adjust", ct);
        var reference = CatalogRules.Code(input.Reference);
        var normalized = reference.ToUpperInvariant();
        var reason = CatalogRules.Required(input.Reason, 1000, "Reason");
        var actorId = Customer();
        var hash = AfterSalesHash(new { OrderId = orderId, ActorId = actorId, Reference = normalized, Reason = reason, input.CloseRemainingCod,
            Items = input.Items.OrderBy(i => i.ShipmentId).ThenBy(i => i.OrderItemId).ToArray() });
        return await work.ExecuteAsync(async () =>
        {
            await Gate(ct);
            var prior = await db.PhysicalReturns.Include(r => r.Items).SingleOrDefaultAsync(r => r.OperationId == input.OperationId, ct);
            if (prior is not null)
            {
                if (prior.OrderId != orderId || prior.ActorId != actorId || prior.RequestHash != hash)
                    throw new ConflictException("Return OperationId was already used for another actor, order or content.");
                var current = await db.Orders.SingleAsync(o => o.Id == orderId, ct);
                return new ReturnResult(MapReturn(prior), current.Version, current.Status == OrderStatus.Closed);
            }
            var order = await RenewalOrder(orderId, false, ct);
            CheckVersion(input.OrderVersion, order.Version);
            if (await db.PhysicalReturns.AnyAsync(r => r.NormalizedReference == normalized, ct))
                throw new ConflictException("This return reference has already been recorded.");
            var shipped = await db.Set<ShipmentItem>().Where(i => i.OrderId == orderId).ToListAsync(ct);
            var origins = shipped.ToDictionary(i => (i.ShipmentId, i.OrderItemId));
            var returned = (await db.PhysicalReturnItems.Where(i => i.OrderId == orderId).ToListAsync(ct))
                .GroupBy(i => (i.ShipmentId, i.OrderItemId)).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
            foreach (var line in input.Items)
                if (!origins.TryGetValue((line.ShipmentId, line.OrderItemId), out var origin) ||
                    line.Quantity > origin.Quantity - returned.GetValueOrDefault((line.ShipmentId, line.OrderItemId)))
                    throw new ConflictException("A return must be within its order's remaining dispatched shipment quantity.");
            if (input.CloseRemainingCod && (order.PaymentMethod != OrderPaymentMethod.CashOnDelivery || order.PaymentStatus != OrderPaymentStatus.Unpaid ||
                order.Status == OrderStatus.Closed || shipped.Count == 0 || shipped.Any(i => returned.GetValueOrDefault((i.ShipmentId, i.OrderItemId)) +
                    (input.Items.SingleOrDefault(l => l.ShipmentId == i.ShipmentId && l.OrderItemId == i.OrderItemId)?.Quantity ?? 0) != i.Quantity)))
                throw new ConflictException("CloseRemainingCod requires an unpaid COD order with every dispatched quantity received back.");
            // Lock all order products before any balance, including when closing holds on other lines.
            await LockProducts(order.Items.Select(i => i.ProductId), ct);
            var movementIds = input.Items.Select(i => origins[(i.ShipmentId, i.OrderItemId)].StockMovementId).ToArray();
            var movements = await db.StockMovements.Where(m => movementIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct);
            var balanceIds = movements.Values.Select(m => m.InventoryItemId).Distinct().Order().ToArray();
            foreach (var id in balanceIds)
                await db.InventoryItems.Where(i => i.Id == id).ExecuteUpdateAsync(s => s.SetProperty(i => i.Version, i => i.Version), ct);
            var balances = await db.InventoryItems.Where(i => balanceIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
            var lines = new List<PhysicalReturnItem>();
            foreach (var line in input.Items)
            {
                var origin = origins[(line.ShipmentId, line.OrderItemId)];
                if (!origin.StockMovementId.HasValue)
                {
                    if (line.RestockQuantity != 0) throw new DomainException("Hàng không theo dõi tồn kho không cần nhập lại số lượng kho.");
                    lines.Add(new(line.ShipmentId, line.OrderItemId, origin.VariantId, line.Quantity, 0, null));
                    continue;
                }
                if (!movements.TryGetValue(origin.StockMovementId.Value, out var dispatch) || dispatch.Delta != -origin.Quantity ||
                    !balances.TryGetValue(dispatch.InventoryItemId, out var balance) || balance.VariantId != origin.VariantId)
                    throw new ConflictException("The original dispatch evidence is inconsistent; review before restocking.");
                Guid? movementId = null;
                if (line.RestockQuantity > 0)
                {
                    var movement = balance.Adjust(Guid.NewGuid(), line.RestockQuantity, $"Return order {order.Number}", actorId);
                    db.StockMovements.Add(movement);
                    movementId = movement.Id;
                }
                lines.Add(new(line.ShipmentId, line.OrderItemId, origin.VariantId, line.Quantity, line.RestockQuantity, movementId));
            }
            var receipt = new PhysicalReturn(orderId, input.OperationId, hash, reference, reason, Now, input.CloseRemainingCod, actorId, lines);
            db.PhysicalReturns.Add(receipt);
            if (input.CloseRemainingCod) await CloseAfterSales(order, ct);
            order.Touch();
            return new ReturnResult(MapReturn(receipt), order.Version, order.Status == OrderStatus.Closed);
        }, ct);
    }
}
