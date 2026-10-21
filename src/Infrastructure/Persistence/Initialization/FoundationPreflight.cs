using System.Text.Json;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Infrastructure.Persistence.Initialization;

/// <summary>Read-only integrity checks for the current baseline, after migrations have been applied.</summary>
internal static class FoundationPreflight
{
    private static readonly JsonSerializerOptions ReportOptions = new() { WriteIndented = true };

    internal static async Task RunAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var actions = await db.Actions.AsNoTracking()
            .Select(x => new CapabilityCode(x.Id, x.Code, x.NormalizedCode)).ToListAsync(cancellationToken);
        var functions = await db.Functions.AsNoTracking()
            .Select(x => new CapabilityCode(x.Id, x.Code, x.NormalizedCode)).ToListAsync(cancellationToken);
        var actionReport = CheckCodes(actions);
        var functionReport = CheckCodes(functions);
        var groups = await db.FunctionGroups.AsNoTracking()
            .Select(x => new CapabilityCode(x.Id, x.Code, x.NormalizedCode)).ToListAsync(cancellationToken);
        var groupReport = CheckCodes(groups);

        // Check historical grants as well. A damaged import must never be repaired by inventing
        // new action/function pairs, since that could activate additional permissions.
        var missingPairs = await db.Permissions.IgnoreQueryFilters().AsNoTracking()
            .Where(grant => !db.ActionInFunctions.Any(pair => pair.ActionId == grant.ActionId && pair.FunctionId == grant.FunctionId))
            .Select(grant => new { GrantId = grant.Id, grant.RoleId, grant.FunctionId, grant.ActionId })
            .ToListAsync(cancellationToken);

        var report = new Dictionary<string, object>
        {
            ["Actions"] = actionReport,
            ["Functions"] = functionReport,
            ["FunctionGroups"] = groupReport,
            ["InvalidGrantPairs"] = missingPairs.Count,
            ["InvalidGrantPairRows"] = missingPairs,
            ["Identity.Users"] = await db.Users.LongCountAsync(cancellationToken),
            ["Identity.Roles"] = await db.Roles.LongCountAsync(cancellationToken),
            ["Identity.Permissions"] = await db.Permissions.IgnoreQueryFilters().LongCountAsync(cancellationToken),
            ["Identity.UserSessions"] = await db.UserSessions.LongCountAsync(cancellationToken),
            ["Auditing.AuditTrails"] = await db.Trails.LongCountAsync(cancellationToken),
            ["Catalog.Products"] = await db.Products.IgnoreQueryFilters().LongCountAsync(cancellationToken),
            ["Catalog.Categories"] = await db.Categories.IgnoreQueryFilters().LongCountAsync(cancellationToken),
            ["Notifications.Notifications"] = await db.Notifications.IgnoreQueryFilters().LongCountAsync(cancellationToken),
            ["Notifications.NotificationReceipts"] = await db.NotificationReceipts.LongCountAsync(cancellationToken)
        };

        Console.WriteLine(JsonSerializer.Serialize(report, ReportOptions));
        if (!actionReport.IsValid || !functionReport.IsValid || !groupReport.IsValid || missingPairs.Count != 0)
        {
            throw new ConflictException("Integrity check failed. Reconcile the reported IDs explicitly; no data was changed.");
        }
    }

    private static CodeReport CheckCodes(IReadOnlyCollection<CapabilityCode> codes)
    {
        var collisions = codes.GroupBy(x => x.Code.ToUpperInvariant(), StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Select(x => x.Id).ToArray()).ToArray();
        var malformed = codes.Where(x => string.IsNullOrWhiteSpace(x.Code) || x.Code.Length > 100 ||
            x.Code.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_' && character != '-'))
            .Select(x => x.Id).ToArray();
        var unnormalized = codes.Where(x => !string.Equals(x.NormalizedCode, x.Code.ToUpperInvariant(), StringComparison.Ordinal))
            .Select(x => x.Id).ToArray();
        return new CodeReport(codes.Count, collisions, malformed, unnormalized);
    }

    private sealed record CapabilityCode(Guid Id, string Code, string NormalizedCode);

    private sealed record CodeReport(int Count, Guid[][] CollidingIds, Guid[] InvalidCodeIds, Guid[] InvalidNormalizedCodeIds)
    {
        public bool IsValid => CollidingIds.Length == 0 && InvalidCodeIds.Length == 0 && InvalidNormalizedCodeIds.Length == 0;
    }
}
