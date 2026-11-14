using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Ordering.Shared;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.DataProtection;

namespace Boilerate.Application.Renewals;

public sealed record SetRenewalTargetRequest(
    Guid OrderId,
    Guid Id,
    SetRenewalTarget Input) : IRequest<RenewalDto>;

public sealed class SetRenewalTargetRequestValidator : CustomValidator<SetRenewalTargetRequest>
{
    public SetRenewalTargetRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Input).NotNull();
    }
}

public sealed class SetRenewalTargetRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    TimeProvider clock,
    IDataProtectionProvider protection) : IRequestHandler<SetRenewalTargetRequest, RenewalDto>
{
    public Task<RenewalDto> Handle(SetRenewalTargetRequest request, CancellationToken ct) =>
        RenewalWorkflowHelper.SetTargetForBuyerAsync(db, work, protection, clock.GetUtcNow().UtcDateTime,
            request.OrderId, request.Id, request.Input, OrderingAuthHelper.RequireCustomer(actor), null, ct);
}
