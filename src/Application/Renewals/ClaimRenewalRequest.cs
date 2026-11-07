using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Ordering.Shared;
using Boilerate.Domain.Renewals;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Boilerate.Application.Renewals;

public sealed record ClaimRenewalRequest(Guid Id, ClaimRenewal Input) : IRequest<RenewalAdminDto>;

public sealed class ClaimRenewalRequestValidator : CustomValidator<ClaimRenewalRequest>
{
    public ClaimRenewalRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Input).NotNull();
    }
}

public sealed class ClaimRenewalRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IUserService users,
    IOptions<OrderingSettings> settings,
    TimeProvider clock) : IRequestHandler<ClaimRenewalRequest, RenewalAdminDto>
{
    public async Task<RenewalAdminDto> Handle(ClaimRenewalRequest request, CancellationToken ct)
    {
        Guid actorId = await OrderingAuthHelper.RequirePermissionAsync(actor, users, "Renewal", "Process", ct);
        DateTime now = clock.GetUtcNow().UtcDateTime;
        return await RenewalWorkflowHelper.ChangeRenewalAsync(
            db,
            work,
            request.Id,
            request.Input.Version,
            request.Input.OperationId,
            actorId,
            now,
            "Claim",
            new { },
            true,
            async (renewal, _) =>
            {
                var competing = await db.RenewalRequests
                    .Where(r => r.Id != request.Id &&
                        r.TargetKey == renewal.TargetKey &&
                        (r.Status == RenewalStatus.Claimed || r.Status == RenewalStatus.PendingVerification))
                    .ToListAsync(ct);
                foreach (var other in competing)
                {
                    if (!other.ReleaseExpired(now))
                    {
                        throw new ConflictException("Another renewal for this account is claimed or awaiting verification.");
                    }

                    OrderFulfillmentCoordinator.RecordRenewalOperation(
                        db,
                        other,
                        Guid.NewGuid(),
                        actorId,
                        "ClaimExpired",
                        OrderFulfillmentCoordinator.RenewalHash(new { OtherRequestId = other.Id }),
                        now);
                }

                if (renewal.ReleaseExpired(now))
                {
                    OrderFulfillmentCoordinator.RecordRenewalOperation(
                        db,
                        renewal,
                        Guid.NewGuid(),
                        actorId,
                        "ClaimExpired",
                        OrderFulfillmentCoordinator.RenewalHash(new { RequestId = request.Id }),
                        now);
                }

                if (competing.Count > 0)
                {
                    await db.SaveChangesAsync(ct);
                }

                renewal.Claim(actorId, now, settings.Value.RenewalClaimMinutes);
            },
            ct);
    }
}

