using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Ordering.Shared;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace Boilerate.Application.Ordering.Cart;

public sealed record SetMyCartDiscountRequest(SetCartDiscount Input) : IRequest<CartDto>;

public sealed class SetMyCartDiscountRequestValidator : CustomValidator<SetMyCartDiscountRequest>
{
    public SetMyCartDiscountRequestValidator()
    {
        RuleFor(x => x.Input).NotNull();
    }
}

public sealed class SetMyCartDiscountRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IOptions<OrderingSettings> settings,
    TimeProvider clock) : IRequestHandler<SetMyCartDiscountRequest, CartDto>
{
    public Task<CartDto> Handle(SetMyCartDiscountRequest request, CancellationToken ct) =>
        CartQuoteBuilder.SetDiscountForAsync(
            db,
            work,
            settings.Value,
            clock.GetUtcNow().UtcDateTime,
            OrderingAuthHelper.RequireCustomer(actor),
            request.Input,
            ct);
}

