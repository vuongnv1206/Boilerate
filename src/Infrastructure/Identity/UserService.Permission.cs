using Boilerate.Domain.Identity;
using Boilerate.Application.Identity.Users;
using Boilerate.Shared.Authorization;
using Microsoft.EntityFrameworkCore;
namespace Boilerate.Infrastructure.Identity;
internal partial class UserService
{
    private readonly Dictionary<string, bool> _permissionDecisions = new(StringComparer.Ordinal);
    public async Task<AuthorizationContextDto> GetAuthorizationAsync(string userId, CancellationToken cancellationToken) =>
        new(await GetPermissionsAsync(userId, cancellationToken), await _guard.IsAdminAsync(userId, cancellationToken));
    private IQueryable<Permission> EffectiveGrants(string userId) => _db.Permissions.AsNoTracking().Where(grant =>
        grant.DeletedOn == null && grant.Function.IsActive && grant.Action.IsActive &&
        _db.Users.Any(user => user.Id == userId && user.IsActive) &&
        _db.UserRoles.Any(assignment => assignment.UserId == userId && assignment.RoleId == grant.RoleId) &&
        _db.ActionInFunctions.Any(pair => pair.ActionId == grant.ActionId && pair.FunctionId == grant.FunctionId && pair.IsActive));
    public async Task<List<string>> GetPermissionsAsync(string userId, CancellationToken cancellationToken) =>
        await EffectiveGrants(userId).Select(g => "Permissions." + g.Function.Code + "." + g.Action.Code)
            .Distinct().OrderBy(x => x).ToListAsync(cancellationToken);
    public async Task<bool> HasPermissionAsync(string userId, string permission, CancellationToken cancellationToken = default)
    {
        var parts = permission.Split('.');
        if (parts.Length != 3 || parts[0] != "Permissions") return false;
        var function = parts[1].ToUpperInvariant(); var action = parts[2].ToUpperInvariant();
        var key = userId + ":" + function + ":" + action;
        if (_permissionDecisions.TryGetValue(key, out var allowed)) return allowed;
        allowed = await EffectiveGrants(userId).AnyAsync(g => g.Function.NormalizedCode == function && g.Action.NormalizedCode == action, cancellationToken);
        _permissionDecisions[key] = allowed;
        return allowed;
    }
}
