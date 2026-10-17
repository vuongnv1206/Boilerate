using Boilerate.Infrastructure.Commerce;
using Boilerate.Infrastructure.Auth;
using Boilerate.Infrastructure.BackgroundJobs;
using Boilerate.Infrastructure.Caching;
using Boilerate.Infrastructure.Common;
using Boilerate.Infrastructure.FileStorage;
using Boilerate.Infrastructure.Identity;
using Boilerate.Infrastructure.Mailing;
using Boilerate.Infrastructure.Middleware;
using Boilerate.Infrastructure.Persistence;
using Boilerate.Infrastructure.Persistence.Initialization;
using Boilerate.Infrastructure.BlobStorage;
using Boilerate.Infrastructure.Exporters;
using Boilerate.Infrastructure.Notifications;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerate.Infrastructure;

public static class Startup
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
        return services
            .AddDeploymentRuntime(config)
            .AddPersistence()
            .AddCommerce(config)
            .AddIdentity(config)
            .AddAuth(config)
            .AddCaching(config)
            .AddMailing(config)
            .AddBackgroundJobs(config)
            .AddBlobStorage(config)
            .AddExporters()
            .AddNotifications(config)
            .AddExceptionMiddleware()
            .AddRouting(options => options.LowercaseUrls = true)
            .AddServices();
    }

    public static IApplicationBuilder UseInfrastructure(
        this IApplicationBuilder builder,
        IConfiguration config)
    {
        if (config.GetValue<bool>("ReverseProxy:Enabled")) builder.UseForwardedHeaders();
        return builder
            .UseExceptionMiddleware()
            .UseRouting()
            .UseCors("storefront")
            .UseRateLimiter()
            .UseFileStorage()
            .UseAuth()
            .UseHangfireDashboard(config);
    }


    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapControllers();
        builder.MapNotifications();
        return builder;
    }

}
