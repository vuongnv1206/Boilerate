using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Ordering.Shared;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Renewals;

public sealed record RevealAdminRenewalTargetRequest(Guid Id) : IRequest<RenewalContentDto>;

public sealed class RevealAdminRenewalTargetRequestValidator : CustomValidator<RevealAdminRenewalTargetRequest>
{
    public RevealAdminRenewalTargetRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class RevealAdminRenewalTargetRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IUserService users,
    TimeProvider clock,
    IDataProtectionProvider protection) : IRequestHandler<RevealAdminRenewalTargetRequest, RenewalContentDto>
{
    public async Task<RenewalContentDto> Handle(RevealAdminRenewalTargetRequest request, CancellationToken ct)
    {
        Guid actorId = await OrderingAuthHelper.RequirePermissionAsync(actor, users, "Renewal", "Reveal", ct);
        DateTime now = clock.GetUtcNow().UtcDateTime;
        return await work.ExecuteAsync(async () =>
        {
            var renewal = await db.RenewalRequests.SingleOrDefaultAsync(r => r.Id == request.Id, ct)
                ?? throw new NotFoundException("Renewal not found.");
            return RenewalWorkflowHelper.RevealTarget(db, protection, actorId, now, renewal);
        }, ct);
    }
}

