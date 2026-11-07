using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Ordering.Shared;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace Boilerate.Application.Storefront.Cart;

public sealed record SaveGuestCartRequest(Guid GuestId, SaveCart Input) : IRequest<CartDto>;

public sealed class SaveGuestCartRequestValidator : CustomValidator<SaveGuestCartRequest>
{
    public SaveGuestCartRequestValidator()
    {
        RuleFor(x => x.Input).NotNull();
    }
}

public sealed class SaveGuestCartRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    IOptions<OrderingSettings> settings,
    TimeProvider clock) : IRequestHandler<SaveGuestCartRequest, CartDto>
{
    public Task<CartDto> Handle(SaveGuestCartRequest request, CancellationToken ct) =>
        CartQuoteBuilder.SaveCartForAsync(
            db,
            work,
            settings.Value,
            clock.GetUtcNow().UtcDateTime,
            OrderingAuthHelper.RequireGuestId(request.GuestId),
            request.Input,
            ct);
}

