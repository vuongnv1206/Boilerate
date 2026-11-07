using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Ordering.Shared;
using Boilerate.Application.Renewals;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Shipping;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.AfterSales;

public sealed record ReceivePhysicalReturnRequest(
    Guid OrderId,
    ReceiveReturn Input) : IRequest<ReturnResult>;

public sealed class ReceivePhysicalReturnRequestValidator : CustomValidator<ReceivePhysicalReturnRequest>
{
    public ReceivePhysicalReturnRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
    }
}

public sealed class ReceivePhysicalReturnRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IUserService users,
    TimeProvider clock) : IRequestHandler<ReceivePhysicalReturnRequest, ReturnResult>
{
    public async Task<ReturnResult> Handle(ReceivePhysicalReturnRequest request, CancellationToken ct)
    {
        Guid actorId = await OrderingAuthHelper.RequirePermissionAsync(actor, users, "Return", "Receive", ct);
        var input = request.Input;
        if (input.OperationId == Guid.Empty ||
            input.Items is null ||
            input.Items.Count is < 1 or > 50 ||
            input.Items.Any(i =>
                i is null ||
                i.ShipmentId == Guid.Empty ||
                i.OrderItemId == Guid.Empty ||
                i.Quantity is < 1 or > 999 ||
                i.RestockQuantity < 0 ||
                i.RestockQuantity > i.Quantity) ||
            input.Items.Select(i => (i.ShipmentId, i.OrderItemId)).Distinct().Count() != input.Items.Count)
        {
            throw new DomainException("Returns require 1 to 50 distinct shipment lines with valid received/restock quantities.");
        }

        if (input.Items.Any(i => i.RestockQuantity > 0))
        {
            await OrderingAuthHelper.RequirePermissionAsync(actor, users, "Inventory", "Adjust", ct);
        }

        string reference = CatalogRules.Code(input.Reference);
        string normalized = reference.ToUpperInvariant();
        string reason = CatalogRules.Required(input.Reason, 1000, "Reason");
        DateTime now = clock.GetUtcNow().UtcDateTime;
        string hash = AfterSalesHelper.Hash(new
        {
            request.OrderId,
            ActorId = actorId,
            Reference = normalized,
            Reason = reason,
            input.CloseRemainingCod,
            Items = input.Items.OrderBy(i => i.ShipmentId).ThenBy(i => i.OrderItemId).ToArray(),
        });

        return await work.ExecuteAsync(async () =>
        {
            await OrderingAuthHelper.AcquireOrderingGateAsync(db, ct);
            var prior = await db.PhysicalReturns
                .Include(r => r.Items)
                .SingleOrDefaultAsync(r => r.OperationId == input.OperationId, ct);
            if (prior is not null)
            {
                if (prior.OrderId != request.OrderId || prior.ActorId != actorId || prior.RequestHash != hash)
                {
                    throw new ConflictException("Return OperationId was already used for another actor, order or content.");
                }

                var current = await db.Orders.SingleAsync(o => o.Id == request.OrderId, ct);
                return new ReturnResult(AfterSalesHelper.MapReturn(prior), current.Version, current.Status == OrderStatus.Closed);
            }

            var order = await RenewalWorkflowHelper.LoadRenewalOrderAsync(db, request.OrderId, null, ct);
            CommerceConcurrency.CheckVersion(input.OrderVersion, order.Version);
            if (await db.PhysicalReturns.AnyAsync(r => r.NormalizedReference == normalized, ct))
            {
                throw new ConflictException("This return reference has already been recorded.");
            }

            var shipped = await db.Set<ShipmentItem>().Where(i => i.OrderId == request.OrderId).ToListAsync(ct);
            var origins = shipped.ToDictionary(i => (i.ShipmentId, i.OrderItemId));
            var returned = (await db.PhysicalReturnItems.Where(i => i.OrderId == request.OrderId).ToListAsync(ct))
                .GroupBy(i => (i.ShipmentId, i.OrderItemId))
                .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
            foreach (var line in input.Items)
            {
                if (!origins.TryGetValue((line.ShipmentId, line.OrderItemId), out var origin) ||
                    line.Quantity > origin.Quantity - returned.GetValueOrDefault((line.ShipmentId, line.OrderItemId)))
                {
                    throw new ConflictException("A return must be within its order's remaining dispatched shipment quantity.");
                }
            }

            if (input.CloseRemainingCod &&
                (order.PaymentMethod != OrderPaymentMethod.CashOnDelivery ||
                 order.PaymentStatus != OrderPaymentStatus.Unpaid ||
                 order.Status == OrderStatus.Closed ||
                 shipped.Count == 0 ||
                 shipped.Any(i =>
                    returned.GetValueOrDefault((i.ShipmentId, i.OrderItemId)) +
                    (input.Items.SingleOrDefault(l => l.ShipmentId == i.ShipmentId && l.OrderItemId == i.OrderItemId)?.Quantity ?? 0) != i.Quantity)))
            {
                throw new ConflictException("CloseRemainingCod requires an unpaid COD order with every dispatched quantity received back.");
            }

            await OrderStockCoordinator.LockProductsAsync(db, order.Items.Select(i => i.ProductId), ct);
            var movementIds = input.Items.Select(i => origins[(i.ShipmentId, i.OrderItemId)].StockMovementId).ToArray();
            var movements = await db.StockMovements.Where(m => movementIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct);
            var balanceIds = movements.Values.Select(m => m.InventoryItemId).Distinct().Order().ToArray();
            foreach (Guid id in balanceIds)
            {
                await db.InventoryItems
                    .Where(i => i.Id == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(i => i.Version, i => i.Version), ct);
            }

            var balances = await db.InventoryItems.Where(i => balanceIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
            var lines = new List<PhysicalReturnItem>();
            foreach (var line in input.Items)
            {
                var origin = origins[(line.ShipmentId, line.OrderItemId)];
                if (!origin.StockMovementId.HasValue)
                {
                    if (line.RestockQuantity != 0)
                    {
                        throw new DomainException("Hàng không theo dõi tồn kho không cần nhập lại số lượng kho.");
                    }

                    lines.Add(new PhysicalReturnItem(line.ShipmentId, line.OrderItemId, origin.VariantId, line.Quantity, 0, null));
                    continue;
                }

                if (!movements.TryGetValue(origin.StockMovementId.Value, out var dispatch) ||
                    dispatch.Delta != -origin.Quantity ||
                    !balances.TryGetValue(dispatch.InventoryItemId, out var balance) ||
                    balance.VariantId != origin.VariantId)
                {
                    throw new ConflictException("The original dispatch evidence is inconsistent; review before restocking.");
                }

                Guid? movementId = null;
                if (line.RestockQuantity > 0)
                {
                    var movement = balance.Adjust(Guid.NewGuid(), line.RestockQuantity, $"Return order {order.Number}", actorId);
                    db.StockMovements.Add(movement);
                    movementId = movement.Id;
                }

                lines.Add(new PhysicalReturnItem(line.ShipmentId, line.OrderItemId, origin.VariantId, line.Quantity, line.RestockQuantity, movementId));
            }

            var receipt = new PhysicalReturn(
                request.OrderId,
                input.OperationId,
                hash,
                reference,
                reason,
                now,
                input.CloseRemainingCod,
                actorId,
                lines);
            db.PhysicalReturns.Add(receipt);
            if (input.CloseRemainingCod)
            {
                await OrderFulfillmentCoordinator.CloseAfterSalesAsync(db, order, actorId, now, ct);
            }

            order.Touch();
            return new ReturnResult(AfterSalesHelper.MapReturn(receipt), order.Version, order.Status == OrderStatus.Closed);
        }, ct);
    }
}

