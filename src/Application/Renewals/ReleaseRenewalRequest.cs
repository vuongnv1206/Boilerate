using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Ordering.Shared;
using FluentValidation;
using MediatR;

namespace Boilerate.Application.Renewals;

public sealed record ReleaseRenewalRequest(Guid Id, ReleaseRenewal Input) : IRequest<RenewalAdminDto>;

public sealed class ReleaseRenewalRequestValidator : CustomValidator<ReleaseRenewalRequest>
{
    public ReleaseRenewalRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Input).NotNull();
    }
}

public sealed class ReleaseRenewalRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IUserService users,
    TimeProvider clock) : IRequestHandler<ReleaseRenewalRequest, RenewalAdminDto>
{
    public async Task<RenewalAdminDto> Handle(ReleaseRenewalRequest request, CancellationToken ct)
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
            "Release",
            new { },
            false,
            (renewal, _) =>
            {
                renewal.Release(actorId, request.Input.ClaimToken, now);
                return Task.CompletedTask;
            },
            ct);
    }
}

