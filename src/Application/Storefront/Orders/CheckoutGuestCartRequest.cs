using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Ordering.Shared;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace Boilerate.Application.Storefront.Orders;

public sealed record CheckoutGuestCartRequest(
    Guid GuestId,
    string VerifiedEmail,
    Checkout Input) : IRequest<OrderDto>;

public sealed class CheckoutGuestCartRequestValidator : CustomValidator<CheckoutGuestCartRequest>
{
    public CheckoutGuestCartRequestValidator()
    {
        RuleFor(x => x.Input).NotNull();
    }
}

public sealed class CheckoutGuestCartRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    IOptions<OrderingSettings> settings,
    TimeProvider clock) : IRequestHandler<CheckoutGuestCartRequest, OrderDto>
{
    public Task<OrderDto> Handle(CheckoutGuestCartRequest request, CancellationToken ct)
    {
        string email = OrderingAuthHelper.NormalizeEmail(request.VerifiedEmail);
        if (!string.Equals(OrderingAuthHelper.NormalizeEmail(request.Input.Email), email, StringComparison.Ordinal))
        {
            throw new ForbiddenException("Checkout email must match the verified email.");
        }

        return CheckoutExecutor.CheckoutForAsync(
            db,
            work,
            settings.Value,
            clock.GetUtcNow().UtcDateTime,
            OrderingAuthHelper.RequireGuestId(request.GuestId),
            email,
            request.Input,
            ct);
    }
}

