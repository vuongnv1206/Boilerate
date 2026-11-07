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

public sealed record RevealRenewalEvidenceRequest(Guid Id, Guid AttemptId) : IRequest<RenewalContentDto>;

public sealed class RevealRenewalEvidenceRequestValidator : CustomValidator<RevealRenewalEvidenceRequest>
{
    public RevealRenewalEvidenceRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.AttemptId).NotEmpty();
    }
}

public sealed class RevealRenewalEvidenceRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IUserService users,
    TimeProvider clock,
    IDataProtectionProvider protection) : IRequestHandler<RevealRenewalEvidenceRequest, RenewalContentDto>
{
    public async Task<RenewalContentDto> Handle(RevealRenewalEvidenceRequest request, CancellationToken ct)
    {
        Guid actorId = await OrderingAuthHelper.RequirePermissionAsync(actor, users, "Renewal", "Verify", ct);
        DateTime now = clock.GetUtcNow().UtcDateTime;
        return await work.ExecuteAsync(async () =>
        {
            var attempt = await db.RenewalAttempts
                .SingleOrDefaultAsync(a => a.RequestId == request.Id && a.Id == request.AttemptId, ct)
                ?? throw new NotFoundException("Attempt not found.");
            if (attempt.ProtectedEvidence is null)
            {
                throw new ConflictException("This attempt has not been verified.");
            }

            string content = RenewalWorkflowHelper.EvidenceProtector(protection, request.AttemptId).Unprotect(attempt.ProtectedEvidence);
            RenewalWorkflowHelper.ReadAudit(db, actorId, now, request.AttemptId, "RenewalAttempts", "RevealEvidence");
            return new RenewalContentDto(request.AttemptId, content);
        }, ct);
    }
}

