using Boilerate.Application.Identity.Users;
using Boilerate.Shared.Authorization;
using Hangfire.Dashboard;
using Microsoft.Extensions.DependencyInjection;
namespace Boilerate.Infrastructure.BackgroundJobs;
public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        var id = http.User.GetUserId();
        return http.User.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(id) &&
            http.RequestServices.GetRequiredService<IUserService>()
                .HasPermissionAsync(id, "Permissions.Hangfire.View", http.RequestAborted).GetAwaiter().GetResult();
    }
}
