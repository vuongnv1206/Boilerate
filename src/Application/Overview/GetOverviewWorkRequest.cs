using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Identity.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Overview;

public sealed record GetOverviewWorkRequest : IRequest<OverviewWorkDto>;

public sealed class GetOverviewWorkRequestHandler(
    IApplicationDbContext db,
    ICurrentUser actor,
    IUserService users,
    TimeProvider clock) : IRequestHandler<GetOverviewWorkRequest, OverviewWorkDto>
{
    public async Task<OverviewWorkDto> Handle(GetOverviewWorkRequest request, CancellationToken ct)
    {
        if (!actor.IsAuthenticated() || actor.GetUserId() == Guid.Empty)
        {
            throw new UnauthorizedException("Sign in to view the overview.");
        }

        string userId = actor.GetUserId().ToString();
        bool orders = await users.HasPermissionAsync(userId, "Permissions.Order.View", ct);
        bool renewals = await users.HasPermissionAsync(userId, "Permissions.Renewal.View", ct);
        if (!orders && !renewals)
        {
            throw new ForbiddenException("Order.View or Renewal.View is required.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var groups = new List<OverviewWorkGroup>();
        if (orders)
        {
            foreach (string key in new[] { "awaiting-payment", "awaiting-processing", "needs-review" })
            {
                var query = OverviewQueueFilters.Orders(db.Orders.AsNoTracking(), key, now);
                int total = await query.CountAsync(ct);
                var items = await query.OrderBy(o => o.CreatedOn).ThenBy(o => o.Id).Take(5)
                    .Select(o => new OverviewWorkItem(o.Id, o.Id, o.Number, null, null, (int)o.Status, o.CreatedOn))
                    .ToListAsync(ct);
                groups.Add(new(key, "order", total, Normalize(items)));
            }
        }

        if (renewals)
        {
            foreach (string key in new[] { "awaiting-work", "pending-verification" })
            {
                var query = OverviewQueueFilters.Renewals(db.RenewalRequests.AsNoTracking(), db.Orders, key);
                int total = await query.CountAsync(ct);
                var items = await (
                    from r in query
                    join o in db.Orders on r.OrderId equals o.Id
                    join item in db.OrderItems on r.OrderItemId equals item.Id
                    orderby r.CreatedOn, r.Id
                    select new OverviewWorkItem(r.Id, o.Id, o.Number, item.Name, item.Sku, (int)r.Status, r.CreatedOn))
                    .Take(5)
                    .ToListAsync(ct);
                groups.Add(new("renewal-" + key, "renewal", total, Normalize(items)));
            }
        }

        return new(now, groups);
    }

    private static List<OverviewWorkItem> Normalize(List<OverviewWorkItem> items) => items
        .Select(i => i with { CreatedAtUtc = DateTime.SpecifyKind(i.CreatedAtUtc, DateTimeKind.Utc) })
        .ToList();
}
