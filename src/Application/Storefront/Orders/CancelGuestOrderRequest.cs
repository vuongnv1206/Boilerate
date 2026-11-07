using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Ordering.Shared;
using Boilerate.Domain.Ordering;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Storefront.Orders;

public sealed record CancelGuestOrderRequest(
    string VerifiedEmail,
    Guid Id,
    CancelOrder Input) : IRequest<OrderDto>;

public sealed class CancelGuestOrderRequestValidator : CustomValidator<CancelGuestOrderRequest>
{
    public CancelGuestOrderRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Input).NotNull();
    }
}

public sealed class CancelGuestOrderRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work) : IRequestHandler<CancelGuestOrderRequest, OrderDto>
{
    public async Task<OrderDto> Handle(CancelGuestOrderRequest request, CancellationToken ct)
    {
        string email = OrderingAuthHelper.NormalizeEmail(request.VerifiedEmail);
        return await work.ExecuteAsync(async () =>
        {
            await OrderingAuthHelper.AcquireOrderingGateAsync(db, ct);
            var order = await OrderStockCoordinator.Orders(db)
                .SingleOrDefaultAsync(o => o.Id == request.Id && o.IsGuestOrder && o.NormalizedEmail == email, ct)
                ?? throw new NotFoundException("Order not found.");
            CommerceConcurrency.CheckVersion(request.Input.Version, order.Version);
            if (order.PaymentStatus == OrderPaymentStatus.Paid)
            {
                throw new ConflictException("Paid orders require staff review and cannot be cancelled here.");
            }

            if (order.PhysicalDispatchStarted)
            {
                throw new ConflictException("Dispatched orders require staff review and cannot be cancelled here.");
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

