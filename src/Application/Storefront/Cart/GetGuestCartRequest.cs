using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Ordering.Shared;
using MediatR;
using Microsoft.Extensions.Options;

namespace Boilerate.Application.Storefront.Cart;

public sealed record GetGuestCartRequest(Guid GuestId) : IRequest<CartDto>;

public sealed class GetGuestCartRequestValidator : CustomValidator<GetGuestCartRequest>
{
}

public sealed class GetGuestCartRequestHandler(
    IApplicationDbContext db,
    IOptions<OrderingSettings> settings,
    TimeProvider clock) : IRequestHandler<GetGuestCartRequest, CartDto>
{
    public Task<CartDto> Handle(GetGuestCartRequest request, CancellationToken ct) =>
        CartQuoteBuilder.GetCartForAsync(
            db,
            settings.Value,
            clock.GetUtcNow().UtcDateTime,
            OrderingAuthHelper.RequireGuestId(request.GuestId),
            ct);
}

