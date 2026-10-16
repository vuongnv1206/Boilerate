using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Identity.Users;
using Boilerate.Shared.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Infrastructure.Identity;

internal partial class UserService
{
    /// <summary>
    /// Get user's assigned roles
    /// </summary>
    public async Task<List<UserRoleDto>> GetRolesAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

        _ = user ?? throw new NotFoundException("User Not Found.");

        // Get user's roles
        var userRoles = await _userManager.GetRolesAsync(user);

        // Get all available roles
        var allRoles = await _roleManager.Roles.ToListAsync(cancellationToken);

        var roleDtos = allRoles.Select(role => new UserRoleDto
        {
            RoleId = role.Id,
            RoleName = role.Name!,
            Description = role.Description,
            Enabled = userRoles.Contains(role.Name!) // Check if user has this role
        }).ToList();

        return roleDtos;
    }

    /// <summary>
    /// Assign roles to user
    /// Replaces all current roles with new ones
    /// </summary>
    public async Task<string> AssignRolesAsync(string userId, UserRolesRequest request, CancellationToken cancellationToken)
    {
        if (request.RoleIds is null || request.RoleIds.Distinct().Count() != request.RoleIds.Count)
            throw new FluentValidation.ValidationException("Role IDs must be a distinct list.");
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _guard.LockAsync(cancellationToken);
        var user = await _userManager.FindByIdAsync(userId) ?? throw new NotFoundException("User not found.");
        AclGuard.CheckVersion(request.Version, user.ConcurrencyStamp);
        var roles = await _db.Roles.Where(x => request.RoleIds.Contains(x.Id)).ToListAsync(cancellationToken);
        if (roles.Count != request.RoleIds.Count) throw new FluentValidation.ValidationException("Unknown role ID.");
        var current = await _userManager.GetRolesAsync(user);
        var desired = roles.Select(x => x.Name!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var name in current.Where(x => !desired.Contains(x)))
        {
            var result = await _userManager.RemoveFromRoleAsync(user, name);
            if (!result.Succeeded) throw new ConflictException(string.Join("; ", result.GetErrors()));
        }
        foreach (var name in desired.Except(current, StringComparer.OrdinalIgnoreCase))
        {
            var result = await _userManager.AddToRoleAsync(user, name);
            if (!result.Succeeded) throw new ConflictException(string.Join("; ", result.GetErrors()));
        }
        // Touch the user version even when the requested assignment is unchanged.
        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded) throw new ConflictException(string.Join("; ", update.GetErrors()));
        await _guard.EnsureAdministratorRemainsAsync(cancellationToken);
        _guard.Audit("UserRoles", user.Id, current, desired);
        await _db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken);
        return "User roles updated.";
    }
}
