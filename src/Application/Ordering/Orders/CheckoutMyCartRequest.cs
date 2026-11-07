using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Ordering.Shared;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace Boilerate.Application.Ordering.Orders;

public sealed record CheckoutMyCartRequest(Checkout Input) : IRequest<OrderDto>;

public sealed class CheckoutMyCartRequestValidator : CustomValidator<CheckoutMyCartRequest>
{
    public CheckoutMyCartRequestValidator()
    {
        RuleFor(x => x.Input).NotNull();
    }
}

public sealed class CheckoutMyCartRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IOptions<OrderingSettings> settings,
    TimeProvider clock) : IRequestHandler<CheckoutMyCartRequest, OrderDto>
{
    public Task<OrderDto> Handle(CheckoutMyCartRequest request, CancellationToken ct) =>
        CheckoutExecutor.CheckoutForAsync(
            db,
            work,
            settings.Value,
            clock.GetUtcNow().UtcDateTime,
            OrderingAuthHelper.RequireCustomer(actor),
            null,
            request.Input,
            ct);
}

