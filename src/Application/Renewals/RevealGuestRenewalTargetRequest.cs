using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.DataProtection;

namespace Boilerate.Application.Renewals;

/// <summary>A guest reads back the account they named for a renewal on their own paid order.</summary>
public sealed record RevealGuestRenewalTargetRequest(Guid OrderId, Guid Id, string VerifiedEmail) : IRequest<RenewalContentDto>;

public sealed class RevealGuestRenewalTargetRequestValidator : CustomValidator<RevealGuestRenewalTargetRequest>
{
    public RevealGuestRenewalTargetRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class RevealGuestRenewalTargetRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    TimeProvider clock,
    IDataProtectionProvider protection) : IRequestHandler<RevealGuestRenewalTargetRequest, RenewalContentDto>
{
    public Task<RenewalContentDto> Handle(RevealGuestRenewalTargetRequest request, CancellationToken ct) =>
        RenewalWorkflowHelper.RevealTargetForBuyerAsync(db, work, protection, clock.GetUtcNow().UtcDateTime,
            request.OrderId, request.Id, null, request.VerifiedEmail, ct);
}
