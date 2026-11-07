using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Ordering.Shared;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace Boilerate.Application.Ordering.Cart;

public sealed record SaveMyCartRequest(SaveCart Input) : IRequest<CartDto>;

public sealed class SaveMyCartRequestValidator : CustomValidator<SaveMyCartRequest>
{
    public SaveMyCartRequestValidator()
    {
        RuleFor(x => x.Input).NotNull();
    }
}

public sealed class SaveMyCartRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IOptions<OrderingSettings> settings,
    TimeProvider clock) : IRequestHandler<SaveMyCartRequest, CartDto>
{
    public Task<CartDto> Handle(SaveMyCartRequest request, CancellationToken ct) =>
        CartQuoteBuilder.SaveCartForAsync(
            db,
            work,
            settings.Value,
            clock.GetUtcNow().UtcDateTime,
            OrderingAuthHelper.RequireCustomer(actor),
            request.Input,
            ct);
}

