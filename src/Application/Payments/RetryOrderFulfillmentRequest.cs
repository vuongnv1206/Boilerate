using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Ordering.Shared;
using Boilerate.Domain.Ordering;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Payments;

public sealed record RetryOrderFulfillmentRequest(Guid OrderId, RetryFulfillment Input) : IRequest<OrderDto>;

public sealed class RetryOrderFulfillmentRequestValidator : CustomValidator<RetryOrderFulfillmentRequest>
{
    public RetryOrderFulfillmentRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
    }
}

public sealed class RetryOrderFulfillmentRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IUserService users,
    TimeProvider clock,
    IDataProtectionProvider protection) : IRequestHandler<RetryOrderFulfillmentRequest, OrderDto>
{
    public async Task<OrderDto> Handle(RetryOrderFulfillmentRequest request, CancellationToken ct)
    {
        Guid actorId = await OrderingAuthHelper.RequirePermissionAsync(actor, users, "Order", "Fulfill", ct);
        DateTime now = clock.GetUtcNow().UtcDateTime;
        return await work.ExecuteAsync(async () =>
        {
            await OrderingAuthHelper.AcquireOrderingGateAsync(db, ct);
            var order = await OrderStockCoordinator.Orders(db).SingleOrDefaultAsync(o => o.Id == request.OrderId, ct)
                ?? throw new NotFoundException("Order not found.");
            CommerceConcurrency.CheckVersion(request.Input.Version, order.Version);
            if (order.PaymentStatus != OrderPaymentStatus.Paid || order.Status is OrderStatus.Cancelled or OrderStatus.Closed)
            {
                throw new ConflictException("Only paid, non-cancelled orders can resume fulfillment.");
            }

            if (!await OrderStockCoordinator.HoldDiscountUsageAsync(db, order, ct))
            {
                throw new ConflictException("Mã giảm giá đã hết lượt; cần đối chiếu đơn trước khi tiếp tục xử lý.");
            }

            await OrderFulfillmentCoordinator.EnsureRenewalRequestsAsync(db, order, actorId, now, ct);
            if (order.FulfillmentStatus == OrderFulfillmentStatus.NeedsReview)
            {
                if (order.PhysicalDispatchStarted)
                {
                    throw new ConflictException("A dispatched order requires shipment/return review and cannot reacquire its full stock.");
                }

                await OrderFulfillmentCoordinator.FulfillPaidOrderAsync(db, protection, order, actorId, now, ct);
            }

            return OrderFulfillmentCoordinator.MapOrder(order);
        }, ct);
    }
}

