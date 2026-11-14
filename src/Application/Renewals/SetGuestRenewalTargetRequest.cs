using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.DataProtection;

namespace Boilerate.Application.Renewals;

/// <summary>A guest names the account their renewal applies to, on an order of their verified email.</summary>
public sealed record SetGuestRenewalTargetRequest(
    Guid OrderId,
    Guid Id,
    string VerifiedEmail,
    SetRenewalTarget Input) : IRequest<RenewalDto>;

public sealed class SetGuestRenewalTargetRequestValidator : CustomValidator<SetGuestRenewalTargetRequest>
{
    public SetGuestRenewalTargetRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Input).NotNull();
    }
}

public sealed class SetGuestRenewalTargetRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    TimeProvider clock,
    IDataProtectionProvider protection) : IRequestHandler<SetGuestRenewalTargetRequest, RenewalDto>
{
    public Task<RenewalDto> Handle(SetGuestRenewalTargetRequest request, CancellationToken ct) =>
        RenewalWorkflowHelper.SetTargetForBuyerAsync(db, work, protection, clock.GetUtcNow().UtcDateTime,
            request.OrderId, request.Id, request.Input, null, request.VerifiedEmail, ct);
}
