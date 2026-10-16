using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Domain.Auditing;
using Boilerate.Infrastructure.Persistence.Context;
using Boilerate.Shared.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Infrastructure.Identity;

internal sealed class AclGuard : IScopedService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _actor;
    private readonly ISerializerService _serializer;

    public AclGuard(ApplicationDbContext db, ICurrentUser actor, ISerializerService serializer)
        => (_db, _actor, _serializer) = (db, actor, serializer);

    internal async Task RequireAdminAsync(CancellationToken ct)
    {
        if (!_actor.IsAuthenticated() || !await IsAdminAsync(_actor.GetUserId().ToString(), ct))
            throw new ForbiddenException("Central administrator membership is required.");
    }

    internal Task<bool> IsAdminAsync(string userId, CancellationToken ct) =>
        (from assignment in _db.UserRoles
         join role in _db.Roles on assignment.RoleId equals role.Id
         join user in _db.Users on assignment.UserId equals user.Id
         where user.Id == userId && user.IsActive && role.NormalizedName == "ADMIN"
         select assignment).AnyAsync(ct);

    /// <summary>Must execute inside the caller's transaction; the row lock lasts until commit.</summary>
    internal async Task LockAsync(CancellationToken ct)
    {
        var version = Guid.NewGuid().ToString("N");
        if (await _db.AuthorizationLocks.Where(x => x.Id == 1)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Version, version), ct) != 1)
            throw new ConflictException("Authorization lock is missing. Apply foundation migrations.");
        await RequireAdminAsync(ct);
    }

    internal async Task EnsureAdministratorRemainsAsync(CancellationToken ct)
    {
        if (!await (from assignment in _db.UserRoles
                    join role in _db.Roles on assignment.RoleId equals role.Id
                    join user in _db.Users on assignment.UserId equals user.Id
                    where user.IsActive && role.NormalizedName == "ADMIN"
                    select user.Id).AnyAsync(ct))
            throw new ConflictException("The last active administrator cannot be removed or disabled.");
    }

    internal void Audit(string table, string key, object? before, object? after)
    {
        _db.Trails.Add(new Trail
        {
            UserId = _actor.GetUserId(), TableName = table, PrimaryKey = key,
            Type = before is null ? TrailType.Create : after is null ? TrailType.Delete : TrailType.Update,
            OldValues = before is null ? null : _serializer.Serialize(before),
            NewValues = after is null ? null : _serializer.Serialize(after),
            DateTime = DateTime.UtcNow
        });
    }

    internal static void CheckVersion(string? expected, string? actual)
    {
        if (string.IsNullOrWhiteSpace(expected) || expected != actual)
            throw new ConflictException("The record changed. Reload it and submit its current version.");
    }

    internal static string NormalizeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 100 ||
            code.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_' && c != '-'))
            throw new FluentValidation.ValidationException("Code must contain 1-100 ASCII letters, digits, underscores or hyphens.");
        return code.ToUpperInvariant();
    }

    internal static bool IsManagementFunction(string code) =>
        code.ToUpperInvariant() is "ACTION" or "FUNCTION" or "ROLE" or "USER";

    internal static bool IsManagementPair(string function, string action) =>
        function is "ACTION" or "FUNCTION" or "ROLE" ? action is "VIEW" or "CREATE" or "UPDATE" or "DELETE" :
        function == "USER" && action is "VIEW" or "CREATE" or "UPDATE";
}
