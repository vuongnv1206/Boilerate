using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Ordering.Shared;
using FluentValidation;
using MediatR;

namespace Boilerate.Application.Renewals;

public sealed record StartRenewalRequest(Guid Id, StartRenewal Input) : IRequest<RenewalAdminDto>;

public sealed class StartRenewalRequestValidator : CustomValidator<StartRenewalRequest>
{
    public StartRenewalRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Input).NotNull();
    }
}

public sealed class StartRenewalRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IUserService users,
    TimeProvider clock) : IRequestHandler<StartRenewalRequest, RenewalAdminDto>
{
    public async Task<RenewalAdminDto> Handle(StartRenewalRequest request, CancellationToken ct)
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
            "Start",
            new { },
            true,
            (renewal, _) =>
            {
                renewal.Start(actorId, request.Input.ClaimToken, request.Input.OperationId, now);
                return Task.CompletedTask;
            },
            ct);
    }
}

