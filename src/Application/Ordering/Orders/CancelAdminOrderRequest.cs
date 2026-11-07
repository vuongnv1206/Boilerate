using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Ordering.Shared;
using Boilerate.Domain.Ordering;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Ordering.Orders;

public sealed record CancelAdminOrderRequest(Guid Id, CancelOrder Input) : IRequest<OrderDto>;

public sealed class CancelAdminOrderRequestValidator : CustomValidator<CancelAdminOrderRequest>
{
    public CancelAdminOrderRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Input).NotNull();
    }
}

public sealed class CancelAdminOrderRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IUserService users) : IRequestHandler<CancelAdminOrderRequest, OrderDto>
{
    public async Task<OrderDto> Handle(CancelAdminOrderRequest request, CancellationToken ct)
    {
        await OrderingAuthHelper.RequirePermissionAsync(actor, users, "Order", "Cancel", ct);
        return await work.ExecuteAsync(async () =>
        {
            await OrderingAuthHelper.AcquireOrderingGateAsync(db, ct);
            var order = await OrderStockCoordinator.Orders(db)
                .SingleOrDefaultAsync(o => o.Id == request.Id, ct)
                ?? throw new NotFoundException("Order not found.");
            CommerceConcurrency.CheckVersion(request.Input.Version, order.Version);
            if (order.PaymentStatus == OrderPaymentStatus.Paid)
            {
                throw new ConflictException("Paid orders require the refund/review workflow and cannot be cancelled here.");
            }

            if (order.PhysicalDispatchStarted)
            {
                throw new ConflictException("Dispatched orders require a return/review workflow and cannot release stock here.");
            }

            if (order.Status is OrderStatus.Cancelled or OrderStatus.Expired)
            {
                return OrderFulfillmentCoordinator.MapOrder(order);
            }

            await OrderStockCoordinator.ReleaseStockAsync(db, order, ct);
            order.Cancel();
            return OrderFulfillmentCoordinator.MapOrder(order);
        }, ct);
    }
}

