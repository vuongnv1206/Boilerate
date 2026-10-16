using Boilerate.Infrastructure.Notifications.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerate.Infrastructure.Notifications;

public static class Startup
{
    public static IServiceCollection AddNotifications(this IServiceCollection services, IConfiguration config)
    {
        // Add SignalR
        var signalRBuilder = services.AddSignalR();

        // Add Redis backplane for scaling (optional)
        var redisConnection = config.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            signalRBuilder.AddStackExchangeRedis(redisConnection, options =>
            {
                options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("Boilerate.Notifications");
            });
        }

        if (config.GetValue<bool>("Modules:Notifications:Enabled")) services.AddHostedService<NotificationDispatcher>();
        return services;
    }

    public static IEndpointRouteBuilder MapNotifications(this IEndpointRouteBuilder endpoints)
    {
        var config = endpoints.ServiceProvider.GetRequiredService<IConfiguration>();
        if (config.GetValue<bool>("Modules:Notifications:Enabled"))
            endpoints.MapHub<NotificationHub>("/hubs/notifications", options => options.CloseOnAuthenticationExpiration = true);
        return endpoints;
    }
}
