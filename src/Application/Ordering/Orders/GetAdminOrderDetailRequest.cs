using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Ordering.Shared;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Ordering.Orders;

public sealed record GetAdminOrderDetailRequest(Guid Id) : IRequest<OrderDto>;

public sealed class GetAdminOrderDetailRequestValidator : CustomValidator<GetAdminOrderDetailRequest>
{
    public GetAdminOrderDetailRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class GetAdminOrderDetailRequestHandler(
    IApplicationDbContext db,
    ICurrentUser actor,
    IUserService users) : IRequestHandler<GetAdminOrderDetailRequest, OrderDto>
{
    public async Task<OrderDto> Handle(GetAdminOrderDetailRequest request, CancellationToken ct)
    {
        await OrderingAuthHelper.RequirePermissionAsync(actor, users, "Order", "View", ct);
        var order = await OrderStockCoordinator.Orders(db)
            .AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == request.Id, ct)
            ?? throw new NotFoundException("Order not found.");
        return OrderFulfillmentCoordinator.MapOrder(order);
    }
}

