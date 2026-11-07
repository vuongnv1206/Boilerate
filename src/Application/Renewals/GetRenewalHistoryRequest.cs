using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Ordering.Shared;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Renewals;

public sealed record GetRenewalHistoryRequest(Guid Id) : IRequest<List<RenewalHistoryDto>>;

public sealed class GetRenewalHistoryRequestValidator : CustomValidator<GetRenewalHistoryRequest>
{
    public GetRenewalHistoryRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class GetRenewalHistoryRequestHandler(
    IApplicationDbContext db,
    ICurrentUser actor,
    IUserService users) : IRequestHandler<GetRenewalHistoryRequest, List<RenewalHistoryDto>>
{
    public async Task<List<RenewalHistoryDto>> Handle(GetRenewalHistoryRequest request, CancellationToken ct)
    {
        await OrderingAuthHelper.RequirePermissionAsync(actor, users, "Renewal", "View", ct);
        if (!await db.RenewalRequests.AnyAsync(r => r.Id == request.Id, ct))
        {
            throw new NotFoundException("Renewal not found.");
        }

        var operations = await db.RenewalOperations.AsNoTracking()
            .Where(o => o.RequestId == request.Id)
            .OrderBy(o => o.OccurredAtUtc)
            .ThenBy(o => o.Id)
            .ToListAsync(ct);
        return operations.Select(o => new RenewalHistoryDto(
            o.OperationId,
            o.Action,
            o.ActorId,
            o.StatusAfter,
            CommerceConcurrency.UtcTimestamp(o.OccurredAtUtc))).ToList();
    }
}

