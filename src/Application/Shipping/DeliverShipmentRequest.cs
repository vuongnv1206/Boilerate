using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Ordering.Shared;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.Shipping;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Shipping;

public sealed record DeliverShipmentRequest(
    Guid OrderId,
    Guid ShipmentId,
    DeliverShipment Input) : IRequest<ShipmentDto>;

public sealed class DeliverShipmentRequestValidator : CustomValidator<DeliverShipmentRequest>
{
    public DeliverShipmentRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.ShipmentId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
    }
}

public sealed class DeliverShipmentRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IUserService users,
    TimeProvider clock) : IRequestHandler<DeliverShipmentRequest, ShipmentDto>
{
    public async Task<ShipmentDto> Handle(DeliverShipmentRequest request, CancellationToken ct)
    {
        await OrderingAuthHelper.RequirePermissionAsync(actor, users, "Shipping", "Deliver", ct);
        var r = request.Input;
        if (r.OperationId == Guid.Empty)
        {
            throw new DomainException("A delivery operation ID is required.");
        }

        DateTime now = clock.GetUtcNow().UtcDateTime;
        return await work.ExecuteAsync(async () =>
        {
            await OrderingAuthHelper.AcquireOrderingGateAsync(db, ct);
            var prior = await ShipmentMapper.Shipments(db).SingleOrDefaultAsync(s => s.DeliveryOperationId == r.OperationId, ct);
            if (prior is not null)
            {
                if (prior.Id != request.ShipmentId || prior.OrderId != request.OrderId)
                {
                    throw new ConflictException("Delivery OperationId was already used for a different shipment.");
                }

                return ShipmentMapper.Map(prior, await db.Orders.SingleAsync(o => o.Id == request.OrderId, ct));
            }

            var order = await OrderStockCoordinator.Orders(db).SingleOrDefaultAsync(o => o.Id == request.OrderId, ct)
                ?? throw new NotFoundException("Order not found.");
            var shipment = await ShipmentMapper.Shipments(db)
                .SingleOrDefaultAsync(s => s.Id == request.ShipmentId && s.OrderId == request.OrderId, ct)
                ?? throw new NotFoundException("Shipment not found.");
            CommerceConcurrency.CheckVersion(r.OrderVersion, order.Version);
            CommerceConcurrency.CheckVersion(r.Version, shipment.Version);
            if (shipment.Status == ShipmentStatus.Delivered)
            {
                throw new ConflictException("Shipment was already delivered; replay its original delivery operation.");
            }

            shipment.MarkDelivered(r.OperationId, now);
            await OrderFulfillmentCoordinator.ReconcileOrderFulfillmentAsync(db, order, ct);
            return ShipmentMapper.Map(shipment, order);
        }, ct);
    }
}

