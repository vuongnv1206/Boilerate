using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Ordering;
using Boilerate.Domain.Auditing;
using Boilerate.Domain.Identity;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Boilerate.Infrastructure.Commerce;

public sealed partial class OrderingService
{
    private static readonly string[] DirectOrderHistoryTables =
    [
        "Orders", "OrderItems", "PaymentReceipts", "Shipments", "ShipmentItems",
        "Refunds", "PhysicalReturns", "PhysicalReturnItems"
    ];
    private static readonly string[] RenewalHistoryTables = ["RenewalRequests", "RenewalAttempts", "RenewalOperations"];

    public async Task<IReadOnlyList<OrderHistoryEntryDto>> GetOrderHistoryAsync(Guid orderId, CancellationToken ct)
    {
        await AuthorizeAdmin("View", ct);
        if (!await db.Orders.AsNoTracking().AnyAsync(order => order.Id == orderId, ct))
            throw new NotFoundException("Order not found.");

        var key = orderId.ToString();
        var renewalRequestKeys = await db.RenewalRequests.AsNoTracking()
            .Where(request => request.OrderId == orderId)
            .Select(request => request.Id)
            .ToArrayAsync(ct);
        var directTrails = MatchTrailKeys(
            db.Trails.AsNoTracking().Where(trail => DirectOrderHistoryTables.Contains(trail.TableName)), [key]);
        var renewalTrails = renewalRequestKeys.Length == 0
            ? db.Trails.AsNoTracking().Where(_ => false)
            : MatchTrailKeys(
                db.Trails.AsNoTracking().Where(trail => RenewalHistoryTables.Contains(trail.TableName)),
                renewalRequestKeys.Select(id => id.ToString()).ToArray());
        var trails = await directTrails
            .Concat(renewalTrails)
            .OrderByDescending(trail => trail.DateTime)
            .ThenByDescending(trail => trail.Id)
            .Take(200)
            .ToListAsync(ct);

        var actorIds = trails.Select(trail => trail.UserId).Where(id => id != Guid.Empty).Distinct().ToArray();
        var actorIdStrings = actorIds.Select(id => id.ToString()).ToArray();
        var actorUsers = await db.Set<ApplicationUser>().AsNoTracking()
            .Where(user => actorIdStrings.Contains(user.Id))
            .Select(user => new { user.Id, user.FirstName, user.LastName })
            .ToListAsync(ct);
        var actors = actorUsers.ToDictionary(user => Guid.Parse(user.Id), user =>
            string.IsNullOrWhiteSpace($"{user.FirstName} {user.LastName}".Trim())
                ? "Người dùng hệ thống"
                : $"{user.FirstName} {user.LastName}".Trim());

        return trails.Select(trail => new OrderHistoryEntryDto(
            trail.Id,
            DateTime.SpecifyKind(trail.DateTime, DateTimeKind.Utc),
            trail.Type switch
            {
                TrailType.Create => "Created",
                TrailType.Update => "Updated",
                TrailType.Delete => "Deleted",
                TrailType.Read => "Viewed",
                _ => "Changed"
            },
            actors.GetValueOrDefault(trail.UserId),
            trail.TableName,
            SafeSummary(trail))).ToArray();
    }

    private static IQueryable<Trail> MatchTrailKeys(IQueryable<Trail> query, IReadOnlyCollection<string> keys)
    {
        var parameter = Expression.Parameter(typeof(Trail), "trail");
        Expression? body = null;
        foreach (var key in keys)
        {
            var value = Expression.Constant(key);
            var primaryKey = Expression.Property(parameter, nameof(Trail.PrimaryKey));
            var oldValues = Expression.Property(parameter, nameof(Trail.OldValues));
            var newValues = Expression.Property(parameter, nameof(Trail.NewValues));
            var primaryMatch = Expression.Call(primaryKey, nameof(string.Contains), Type.EmptyTypes, value);
            var oldMatch = Expression.AndAlso(
                Expression.NotEqual(oldValues, Expression.Constant(null, typeof(string))),
                Expression.Call(oldValues, nameof(string.Contains), Type.EmptyTypes, value));
            var newMatch = Expression.AndAlso(
                Expression.NotEqual(newValues, Expression.Constant(null, typeof(string))),
                Expression.Call(newValues, nameof(string.Contains), Type.EmptyTypes, value));
            var keyMatch = Expression.OrElse(primaryMatch, Expression.OrElse(oldMatch, newMatch));
            body = body is null ? keyMatch : Expression.OrElse(body, keyMatch);
        }

        return body is null
            ? query.Where(_ => false)
            : query.Where(Expression.Lambda<Func<Trail, bool>>(body, parameter));
    }

    private static string SafeSummary(Trail trail)
    {
        var action = trail.NewValues?.Contains("DeliveryDeferred", StringComparison.Ordinal) == true
            ? "Fulfillment deferred"
            : trail.NewValues?.Contains("Reveal", StringComparison.Ordinal) == true
                ? "Digital delivery viewed"
                : trail.Type switch
                {
                    TrailType.Create => "Record created",
                    TrailType.Update => "Record updated",
                    TrailType.Delete => "Record deleted",
                    TrailType.Read => "Record viewed",
                    _ => "Record changed"
                };
        return $"{trail.TableName}: {action}";
    }
}
