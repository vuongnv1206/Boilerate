using Boilerate.Application.Identity.Users;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Boilerate.Infrastructure.Auth.Permissions;

/// <summary>
/// Authorizes against current database grants instead of token snapshots.
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IUserService _userService;

    public PermissionAuthorizationHandler(IUserService userService)
    {
        _userService = userService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userId = context.User.GetUserId();

        if (context.User.Identity?.IsAuthenticated != true || string.IsNullOrEmpty(userId))
        {
            return;
        }

        if (await _userService.HasPermissionAsync(userId, requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}
