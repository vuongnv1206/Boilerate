using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Ordering.Shared;
using Boilerate.Application.Overview;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Renewals;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Renewals;

public sealed record GetAdminRenewalsRequest(
    int Page = 1,
    int Size = 20,
    RenewalStatus? Status = null,
    string? Queue = null) : IRequest<Page<RenewalAdminDto>>;

public sealed class GetAdminRenewalsRequestValidator : CustomValidator<GetAdminRenewalsRequest>
{
}

public sealed class GetAdminRenewalsRequestHandler(
    IApplicationDbContext db,
    ICurrentUser actor,
    IUserService users,
    TimeProvider clock) : IRequestHandler<GetAdminRenewalsRequest, Page<RenewalAdminDto>>
{
    public async Task<Page<RenewalAdminDto>> Handle(GetAdminRenewalsRequest request, CancellationToken ct)
    {
        Guid actorId = await OrderingAuthHelper.RequirePermissionAsync(actor, users, "Renewal", "View", ct);
        CommerceConcurrency.ValidatePage(request.Page, request.Size);
        if (request.Status.HasValue && !Enum.IsDefined(request.Status.Value))
        {
            throw new DomainException("Invalid renewal status.");
        }

        DateTime now = clock.GetUtcNow().UtcDateTime;
        var query = RenewalWorkflowHelper.RenewalRequests(db)
            .AsNoTracking()
            .Where(r => !request.Status.HasValue || r.Status == request.Status);
        query = OverviewQueueFilters.Renewals(query, db.Orders, request.Queue);
        int total = await query.CountAsync(ct);
        var requests = await query
            .OrderBy(r => r.CreatedOn)
            .ThenBy(r => r.Id)
            .Skip((request.Page - 1) * request.Size)
            .Take(request.Size)
            .ToListAsync(ct);
        var ids = requests.Select(r => r.OrderItemId).ToArray();
        var items = await db.Set<OrderItem>()
            .AsNoTracking()
            .Where(i => ids.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, ct);
        return new Page<RenewalAdminDto>(
            requests.Select(r => RenewalWorkflowHelper.MapRenewalAdmin(r, items[r.OrderItemId], actorId, now)).ToList(),
            total,
            request.Page,
            request.Size);
    }
}

