using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Ordering.Shared;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Ordering.Orders;

public sealed record GetMyOrderDetailRequest(Guid Id) : IRequest<OrderDto>;

public sealed class GetMyOrderDetailRequestValidator : CustomValidator<GetMyOrderDetailRequest>
{
    public GetMyOrderDetailRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class GetMyOrderDetailRequestHandler(
    IApplicationDbContext db,
    ICurrentUser actor) : IRequestHandler<GetMyOrderDetailRequest, OrderDto>
{
    public async Task<OrderDto> Handle(GetMyOrderDetailRequest request, CancellationToken ct)
    {
        Guid customer = OrderingAuthHelper.RequireCustomer(actor);
        var order = await OrderStockCoordinator.Orders(db)
            .AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == request.Id && o.CustomerId == customer, ct)
            ?? throw new NotFoundException("Order not found.");
        return OrderFulfillmentCoordinator.MapOrder(order);
    }
}

