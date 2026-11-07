using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Ordering.Shared;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Shipping;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Shipping;

public sealed record DispatchShipmentRequest(
    Guid OrderId,
    DispatchShipment Input) : IRequest<ShipmentDto>;

public sealed class DispatchShipmentRequestValidator : CustomValidator<DispatchShipmentRequest>
{
    public DispatchShipmentRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
    }
}

public sealed class DispatchShipmentRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IUserService users,
    TimeProvider clock) : IRequestHandler<DispatchShipmentRequest, ShipmentDto>
{
    public async Task<ShipmentDto> Handle(DispatchShipmentRequest request, CancellationToken ct)
    {
        Guid actorId = await OrderingAuthHelper.RequirePermissionAsync(actor, users, "Shipping", "Dispatch", ct);
        var r = request.Input;
        if (r.OperationId == Guid.Empty ||
            r.Items is null ||
            r.Items.Count is < 1 or > 50 ||
            r.Items.Any(i => i is null || i.OrderItemId == Guid.Empty || i.Quantity is < 1 or > 999) ||
            r.Items.Select(i => i.OrderItemId).Distinct().Count() != r.Items.Count)
        {
            throw new DomainException("A shipment requires an operation ID and 1 to 50 distinct lines with quantities from 1 to 999.");
        }

        string carrier = CatalogRules.Required(r.Carrier, 100, "Carrier");
        string tracking = CatalogRules.Required(r.TrackingNumber, 100, "TrackingNumber");
        string hash = ShipmentHash(request.OrderId, carrier, tracking, r.Items);
        DateTime now = clock.GetUtcNow().UtcDateTime;

        return await work.ExecuteAsync(async () =>
        {
            await OrderingAuthHelper.AcquireOrderingGateAsync(db, ct);
            var prior = await ShipmentMapper.Shipments(db).SingleOrDefaultAsync(s => s.OperationId == r.OperationId, ct);
            if (prior is not null)
            {
                if (prior.OrderId != request.OrderId || prior.RequestHash != hash)
                {
                    throw new ConflictException("OperationId was already used with different shipment content.");
                }

                return ShipmentMapper.Map(prior, await db.Orders.SingleAsync(o => o.Id == request.OrderId, ct));
            }

            var order = await OrderStockCoordinator.Orders(db).SingleOrDefaultAsync(o => o.Id == request.OrderId, ct)
                ?? throw new NotFoundException("Order not found.");
            CommerceConcurrency.CheckVersion(r.OrderVersion, order.Version);
            if (order.Status is OrderStatus.Cancelled or OrderStatus.Expired or OrderStatus.Closed ||
                order.FulfillmentStatus == OrderFulfillmentStatus.NeedsReview ||
                (order.PaymentStatus != OrderPaymentStatus.Paid && order.PaymentMethod != OrderPaymentMethod.CashOnDelivery))
            {
                throw new ConflictException("Only ready paid orders or physical COD orders can dispatch stock.");
            }

            var physical = order.Items.Where(i => i.FulfillmentKind == ProductFulfillmentKind.Physical).ToDictionary(i => i.Id);
            if (r.Items.Any(i => !physical.ContainsKey(i.OrderItemId)))
            {
                throw new ConflictException("Only physical items belonging to this order may be shipped.");
            }

            await OrderStockCoordinator.LockProductsAsync(db, r.Items.Select(i => physical[i.OrderItemId].ProductId), ct);
            var orderItemIds = r.Items.Select(i => i.OrderItemId).ToArray();
            var previouslyShipped = await db.Set<ShipmentItem>()
                .Where(i => orderItemIds.Contains(i.OrderItemId))
                .GroupBy(i => i.OrderItemId)
                .Select(g => new { Id = g.Key, Quantity = g.Sum(i => i.Quantity) })
                .ToDictionaryAsync(i => i.Id, i => i.Quantity, ct);
            var reservations = await db.PhysicalStockReservations
                .Where(p => orderItemIds.Contains(p.OrderItemId))
                .ToDictionaryAsync(p => p.OrderItemId, ct);
            var balanceIds = reservations.Values.Select(p => p.InventoryItemId).Distinct().Order().ToArray();
            foreach (Guid id in balanceIds)
            {
                await db.InventoryItems
                    .Where(i => i.Id == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(i => i.Version, i => i.Version), ct);
            }

            var balances = await db.InventoryItems.Where(i => balanceIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
            foreach (var line in r.Items)
            {
                if (!physical[line.OrderItemId].TrackInventory)
                {
                    if (previouslyShipped.GetValueOrDefault(line.OrderItemId) + line.Quantity > physical[line.OrderItemId].Quantity)
                    {
                        throw new ConflictException("Shipment exceeds the order's remaining quantity.");
                    }

                    continue;
                }

                if (previouslyShipped.GetValueOrDefault(line.OrderItemId) + line.Quantity > physical[line.OrderItemId].Quantity ||
                    !reservations.TryGetValue(line.OrderItemId, out var reservation) ||
                    reservation.State != HoldState.Held ||
                    reservation.Remaining < line.Quantity ||
                    !balances.TryGetValue(reservation.InventoryItemId, out var balance) ||
                    balance.Reserved < line.Quantity ||
                    balance.OnHand < line.Quantity)
                {
                    throw new ConflictException("Shipment exceeds the order's remaining reserved quantity.");
                }
            }

            var lines = new List<ShipmentItem>();
            foreach (var line in r.Items.OrderBy(i => i.OrderItemId))
            {
                var item = physical[line.OrderItemId];
                if (!item.TrackInventory)
                {
                    lines.Add(new ShipmentItem(order.Id, item.Id, item.VariantId, null, line.Quantity));
                    continue;
                }

                var reservation = reservations[line.OrderItemId];
                var movement = balances[reservation.InventoryItemId].ConsumeReserved(
                    Guid.NewGuid(),
                    line.Quantity,
                    $"Dispatch order {order.Number}",
                    actorId);
                reservation.Consume(line.Quantity);
                db.StockMovements.Add(movement);
                lines.Add(new ShipmentItem(order.Id, item.Id, item.VariantId, movement.Id, line.Quantity));
            }

            var shipment = new Shipment(order.Id, r.OperationId, hash, carrier, tracking, now, lines);
            db.Set<Shipment>().Add(shipment);
            order.RegisterPhysicalShipment(false);
            return ShipmentMapper.Map(shipment, order);
        }, ct);
    }

    private static string ShipmentHash(Guid orderId, string carrier, string tracking, IEnumerable<ShipmentLineInput> items) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            OrderId = orderId,
            Carrier = carrier,
            TrackingNumber = tracking,
            Items = items.OrderBy(i => i.OrderItemId).Select(i => new { i.OrderItemId, i.Quantity }),
        }))));
}

