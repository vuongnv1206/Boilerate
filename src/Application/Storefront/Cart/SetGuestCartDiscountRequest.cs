using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Ordering.Shared;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace Boilerate.Application.Storefront.Cart;

public sealed record SetGuestCartDiscountRequest(Guid GuestId, SetCartDiscount Input) : IRequest<CartDto>;

public sealed class SetGuestCartDiscountRequestValidator : CustomValidator<SetGuestCartDiscountRequest>
{
    public SetGuestCartDiscountRequestValidator()
    {
        RuleFor(x => x.Input).NotNull();
    }
}

public sealed class SetGuestCartDiscountRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    IOptions<OrderingSettings> settings,
    TimeProvider clock) : IRequestHandler<SetGuestCartDiscountRequest, CartDto>
{
    public Task<CartDto> Handle(SetGuestCartDiscountRequest request, CancellationToken ct) =>
        CartQuoteBuilder.SetDiscountForAsync(
            db,
            work,
            settings.Value,
            clock.GetUtcNow().UtcDateTime,
            OrderingAuthHelper.RequireGuestId(request.GuestId),
            request.Input,
            ct);
}

