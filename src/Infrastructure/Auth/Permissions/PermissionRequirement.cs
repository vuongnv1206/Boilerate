using Microsoft.AspNetCore.Authorization;

namespace Boilerate.Infrastructure.Auth.Permissions;

/// <summary>
/// Requirement cho Permission-based authorization
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }

    public PermissionRequirement(string permission)
    {
        Permission = permission;
    }
}
