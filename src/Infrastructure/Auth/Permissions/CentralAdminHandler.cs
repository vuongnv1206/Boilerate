using Boilerate.Infrastructure.Identity;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Boilerate.Infrastructure.Auth.Permissions;

internal sealed class CentralAdminRequirement : IAuthorizationRequirement { }

internal sealed class CentralAdminHandler(AclGuard guard) : AuthorizationHandler<CentralAdminRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, CentralAdminRequirement requirement)
    {
        var id = context.User.GetUserId();
        if (context.User.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(id) && await guard.IsAdminAsync(id, default))
            context.Succeed(requirement);
    }
}
