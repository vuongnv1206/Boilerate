using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Ordering.Shared;
using MediatR;
using Microsoft.Extensions.Options;

namespace Boilerate.Application.Ordering.Cart;

public sealed record GetMyCartRequest : IRequest<CartDto>;

public sealed class GetMyCartRequestValidator : CustomValidator<GetMyCartRequest>
{
}

public sealed class GetMyCartRequestHandler(
    IApplicationDbContext db,
    ICurrentUser actor,
    IOptions<OrderingSettings> settings,
    TimeProvider clock) : IRequestHandler<GetMyCartRequest, CartDto>
{
    public Task<CartDto> Handle(GetMyCartRequest request, CancellationToken ct) =>
        CartQuoteBuilder.GetCartForAsync(
            db,
            settings.Value,
            clock.GetUtcNow().UtcDateTime,
            OrderingAuthHelper.RequireCustomer(actor),
            ct);
}

