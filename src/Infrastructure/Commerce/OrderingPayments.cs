using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Ordering;
using Boilerate.Domain.Auditing;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.DigitalInventory;
using Boilerate.Domain.Inventory;
using Boilerate.Domain.Messaging;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Payments;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Infrastructure.Commerce;

public sealed partial class OrderingService
{
    private async Task PaymentPermission(CancellationToken ct)
    {
        var customer = Customer();
        if (!await users.HasPermissionAsync(customer.ToString(), "Permissions.Payment.Confirm", ct))
            throw new ForbiddenException("Payment.Confirm is required.");
    }

    private static string PaymentHash(Guid orderId, ConfirmPayment r, string reference, string currency)
    {
        // Preserve the existing bank hash; include the new source only for COD confirmations.
        var payload = r.Source == "ManualBankTransfer" ? JsonSerializer.Serialize(new
        {
            OrderId = orderId, Reference = reference, Amount = r.Amount.ToString("0.##", CultureInfo.InvariantCulture),
            Currency = currency, r.ReceivedAtUtc
        }) : JsonSerializer.Serialize(new
        {
            OrderId = orderId, Reference = reference, Amount = r.Amount.ToString("0.##", CultureInfo.InvariantCulture),
            Currency = currency, r.ReceivedAtUtc, r.Source
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    public async Task<OrderDto> ConfirmPaymentAsync(Guid orderId, ConfirmPayment r, CancellationToken ct)
    {
        await PaymentPermission(ct);
        var reference = CatalogRules.Code(r.Reference).ToUpperInvariant();
        var currency = CatalogRules.Required(r.Currency, 3, "Currency").ToUpperInvariant();
        if (r.Source is not "ManualBankTransfer" and not "ManualCOD")
            throw new DomainException("Receipt source must be ManualBankTransfer or ManualCOD.");
        CatalogRules.Money(r.Amount, null);
        var hash = PaymentHash(orderId, r, reference, currency);
        return await work.ExecuteAsync(async () =>
        {
            await Gate(ct);
            var prior = await db.PaymentReceipts.SingleOrDefaultAsync(p => p.OperationId == r.OperationId, ct);
            if (prior is not null)
            {
                if (prior.OrderId != orderId || prior.RequestHash != hash)
                    throw new ConflictException("OperationId was already used with different payment content.");
                return MapOrder(await Orders().SingleAsync(o => o.Id == orderId, ct));
            }
            if (await db.PaymentReceipts.AnyAsync(p => p.Source == r.Source && p.NormalizedReference == reference, ct))
                throw new ConflictException("This payment reference has already been recorded for the source.");
            var order = await Orders().SingleOrDefaultAsync(o => o.Id == orderId, ct)
                ?? throw new NotFoundException("Order not found.");
            CheckVersion(r.Version, order.Version);
            if (order.PaymentStatus == OrderPaymentStatus.Paid)
                throw new ConflictException("The order has already been paid.");
            if (r.Amount != order.Total || currency != order.Currency)
                throw new DomainException("Confirm the exact full order total and currency; partial or excess receipts require separate review.");
            if (r.Source == "ManualCOD" && (order.PaymentMethod != OrderPaymentMethod.CashOnDelivery || !await AllPhysicalDelivered(order, ct)))
                throw new ConflictException("COD receipts require a COD order with all physical quantities delivered.");
            var receipt = new PaymentReceipt(order.Id, r.OperationId, hash, r.Reference, r.Amount, currency, r.ReceivedAtUtc, Now, r.Source);
            db.PaymentReceipts.Add(receipt);
            order.RecordPayment(r.ReceivedAtUtc);
            await EnsureRenewalRequests(order, ct);
            if (order.PhysicalDispatchStarted)
                await ReconcileOrderFulfillment(order, ct);
            else if (order.Status is not (OrderStatus.Cancelled or OrderStatus.Closed))
                await FulfillPaidOrder(order, ct);
            return MapOrder(order);
        }, ct);
    }

    public async Task<OrderDto> RetryFulfillmentAsync(Guid orderId, RetryFulfillment r, CancellationToken ct)
    {
        await AuthorizeAdmin("Fulfill", ct);
        return await work.ExecuteAsync(async () =>
        {
            await Gate(ct);
            var order = await Orders().SingleOrDefaultAsync(o => o.Id == orderId, ct)
                ?? throw new NotFoundException("Order not found.");
            CheckVersion(r.Version, order.Version);
            if (order.PaymentStatus != OrderPaymentStatus.Paid || order.Status is OrderStatus.Cancelled or OrderStatus.Closed)
                throw new ConflictException("Only paid, non-cancelled orders can resume fulfillment.");
            await EnsureRenewalRequests(order, ct);
            if (order.FulfillmentStatus == OrderFulfillmentStatus.NeedsReview)
            {
                if (order.PhysicalDispatchStarted)
                    throw new ConflictException("A dispatched order requires shipment/return review and cannot reacquire its full stock.");
                await FulfillPaidOrder(order, ct);
            }
            return MapOrder(order);
        }, ct);
    }

    // Call only inside the ordering transaction. Product locks also serialize digital inventory edits.
    // Check every line first: a failed late-payment reacquisition must not retain new partial holds.
    private async Task FulfillPaidOrder(Order order, CancellationToken ct)
    {
        if (order.Status == OrderStatus.Closed) throw new ConflictException("Closed orders cannot resume fulfillment.");
        await LockProducts(order.Items.Select(i => i.ProductId), ct);
        var physicalIds = order.Items.Where(i => i.FulfillmentKind == ProductFulfillmentKind.Physical).Select(i => i.VariantId).ToArray();
        var balanceIds = await db.InventoryItems.Where(i => physicalIds.Contains(i.VariantId) &&
            db.Set<InventoryLocation>().Any(l => l.Id == i.LocationId && l.Code == "MAIN" && l.IsActive))
            .OrderBy(i => i.Id).Select(i => i.Id).ToArrayAsync(ct);
        foreach (var id in balanceIds)
            await db.InventoryItems.Where(i => i.Id == id).ExecuteUpdateAsync(s => s.SetProperty(i => i.Version, i => i.Version), ct);
        var balances = await db.InventoryItems.Where(i => balanceIds.Contains(i.Id)).ToDictionaryAsync(i => i.VariantId, ct);
        var itemIds = order.Items.Select(i => i.Id).ToArray();
        var reservations = await db.PhysicalStockReservations.Where(r => itemIds.Contains(r.OrderItemId)).ToDictionaryAsync(r => r.OrderItemId, ct);
        var unitsByItem = new Dictionary<Guid, List<DigitalStockItem>>();
        var sufficient = true;
        foreach (var item in order.Items.OrderBy(i => i.VariantId))
        {
            switch (item.FulfillmentKind)
            {
                case ProductFulfillmentKind.Physical:
                    if (!item.TrackInventory) break;
                    var held = reservations.GetValueOrDefault(item.Id);
                    if (!balances.TryGetValue(item.VariantId, out var balance) ||
                        (held?.State == HoldState.Held
                            ? held.InventoryItemId != balance.Id || held.Quantity != item.Quantity || balance.Reserved < held.Quantity
                            : balance.Available < item.Quantity))
                        sufficient = false;
                    break;
                case ProductFulfillmentKind.DigitalUnique:
                    var units = await db.DigitalStockItems.Where(u => u.VariantId == item.VariantId &&
                        (u.State == DigitalStockState.Available ||
                         (u.State == DigitalStockState.Reserved && u.ReservedForOrderItemId == item.Id)))
                        .OrderByDescending(u => u.ReservedForOrderItemId == item.Id).ThenBy(u => u.Id).Take(item.Quantity).ToListAsync(ct);
                    unitsByItem[item.Id] = units;
                    if (units.Count != item.Quantity) sufficient = false;
                    break;
                case ProductFulfillmentKind.DigitalShared:
                    var shared = await db.DigitalStockItems.SingleOrDefaultAsync(u => u.VariantId == item.VariantId && u.State == DigitalStockState.Available, ct);
                    unitsByItem[item.Id] = shared is null ? [] : [shared];
                    if (shared is null) sufficient = false;
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
                var sequence = 0;
                foreach (var unit in unitsByItem[item.Id])
                {
                    var id = Guid.NewGuid();
                    var content = protection.CreateProtector("Boilerate.DigitalInventory.Content.v1", unit.Id.ToString("N"))
                        .Unprotect(unit.ProtectedContent);
                    preparedDeliveries.Add(new(id, order.Id, item.Id, item.VariantId, unit.Id, ++sequence, item.FulfillmentKind,
                        DeliveryProtector(id).Protect(content)));
                }
            }
        }
        catch (CryptographicException)
        {
            // Money is already verified. Missing/corrupt keys defer delivery without undoing the receipt.
            order.MarkFulfillmentReview();
            db.Trails.Add(new Trail
            {
                UserId = actor.GetUserId(), TableName = "Orders", PrimaryKey = order.Id.ToString(),
                Type = TrailType.Update, DateTime = Now,
                NewValues = "{\"Action\":\"DeliveryDeferred\",\"Reason\":\"DigitalContentUnavailable\"}"
            });
            return;
        }
        foreach (var item in order.Items.OrderBy(i => i.VariantId))
        {
            if (item.FulfillmentKind == ProductFulfillmentKind.Physical && item.TrackInventory)
            {
                var balance = balances[item.VariantId];
                var held = reservations.GetValueOrDefault(item.Id);
                if (held?.State == HoldState.Held) continue;
                balance.Reserve(item.Quantity);
                if (held is null)
                    db.PhysicalStockReservations.Add(new(item.Id, item.VariantId, balance.Id, item.Quantity));
                else
                    held.Reacquire();
            }
            else if (item.FulfillmentKind is ProductFulfillmentKind.DigitalUnique or ProductFulfillmentKind.DigitalShared)
            {
                foreach (var unit in unitsByItem[item.Id])
                {
                    if (item.FulfillmentKind == ProductFulfillmentKind.DigitalUnique)
                    {
                        if (unit.State == DigitalStockState.Available) unit.Reserve(item.Id);
                        unit.Deliver(item.Id);
                    }
                }
            }
        }
        db.DigitalDeliveries.AddRange(preparedDeliveries);
        if (preparedDeliveries.Count > 0)
            db.OutboxMessages.Add(OutboxMessage.DigitalDelivery(order.Id, actor.GetUserId()));
        order.CompleteFulfillment(order.Items.All(i => i.FulfillmentKind is ProductFulfillmentKind.DigitalUnique or ProductFulfillmentKind.DigitalShared));
    }

    private IDataProtector DeliveryProtector(Guid id) =>
        protection.CreateProtector("Boilerate.Ordering.DigitalDelivery.Content.v1", id.ToString("N"));

    public async Task<List<DigitalDeliveryDto>> DigitalDeliveriesAsync(Guid orderId, CancellationToken ct)
    {
        var customer = Customer();
        return await work.ExecuteAsync(async () =>
        {
            var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customer, ct)
                ?? throw new NotFoundException("Order not found.");
            if (order.PaymentStatus != OrderPaymentStatus.Paid)
                throw new ConflictException("Digital content is available only after payment confirmation.");
            var deliveries = await db.DigitalDeliveries.AsNoTracking().Where(d => d.OrderId == orderId)
                .OrderBy(d => d.OrderItemId).ThenBy(d => d.Sequence).ToListAsync(ct);
            var result = new List<DigitalDeliveryDto>();
            foreach (var delivery in deliveries)
            {
                result.Add(new(delivery.Id, delivery.OrderItemId, delivery.Sequence,
                    DeliveryProtector(delivery.Id).Unprotect(delivery.ProtectedContent)));
                db.Trails.Add(new Trail
                {
                    UserId = customer, TableName = "DigitalDeliveries", PrimaryKey = delivery.Id.ToString(),
                    Type = TrailType.Read, DateTime = Now, NewValues = "{\"Action\":\"Reveal\"}"
                });
            }
            return result;
        }, ct);
    }
}
