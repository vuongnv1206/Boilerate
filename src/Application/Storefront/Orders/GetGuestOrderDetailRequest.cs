using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Ordering.Shared;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Storefront.Orders;

public sealed record GetGuestOrderDetailRequest(string VerifiedEmail, Guid Id) : IRequest<OrderDto>;

public sealed class GetGuestOrderDetailRequestValidator : CustomValidator<GetGuestOrderDetailRequest>
{
    public GetGuestOrderDetailRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class GetGuestOrderDetailRequestHandler(IApplicationDbContext db)
    : IRequestHandler<GetGuestOrderDetailRequest, OrderDto>
{
    public async Task<OrderDto> Handle(GetGuestOrderDetailRequest request, CancellationToken ct)
    {
        string email = OrderingAuthHelper.NormalizeEmail(request.VerifiedEmail);
        var order = await OrderStockCoordinator.Orders(db)
            .AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == request.Id && o.IsGuestOrder && o.NormalizedEmail == email, ct)
            ?? throw new NotFoundException("Order not found.");
        return OrderFulfillmentCoordinator.MapOrder(order);
    }
}

