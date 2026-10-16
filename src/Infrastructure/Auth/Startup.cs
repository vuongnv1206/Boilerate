using Boilerate.Application.Common.Interfaces;
using Boilerate.Infrastructure.Auth.Jwt;
using Boilerate.Infrastructure.Auth.OAuth2;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerate.Infrastructure.Auth;

internal static class Startup
{
    /// <summary>
    /// Register CurrentUser services.
    /// </summary>
    internal static IServiceCollection AddAuth(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<SecuritySettings>()
            .BindConfiguration(nameof(SecuritySettings))
            .ValidateDataAnnotations()
            .Validate(settings => Uri.TryCreate(settings.PublicBaseUrl, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http", "PublicBaseUrl must be an absolute HTTP(S) URL.")
            .Validate(settings => !(settings.RequireConfirmedAccount || settings.PasswordRecoveryEnabled) ||
                (config.GetValue<bool>("HangfireStorageSettings:Enabled") && !string.IsNullOrWhiteSpace(config["SMTPEmailSettings:SMTPServer"]) &&
                 config["HangfireStorageSettings:StorageProvider"] is "sqlserver" or "postgresql"),
                "Email confirmation/recovery requires configured SMTP and enabled durable background jobs.")
            .ValidateOnStart();

        // Register middleware as Scoped (per request)
        services.AddScoped<CurrentUserMiddleware>();

        services.AddScoped<CurrentUser>();
        services.AddScoped<ICurrentUser>(provider => provider.GetRequiredService<CurrentUser>());
        services.AddScoped<ICurrentUserInitializer>(provider => provider.GetRequiredService<CurrentUser>());

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            options.AddPolicy("CentralAdmin", policy => policy.RequireAuthenticatedUser().AddRequirements(new Permissions.CentralAdminRequirement()));
        });
        services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, Permissions.CentralAdminHandler>();
        return services
            .AddPermissions()
            .AddJwtAuth()
            .AddO2Authentication(config);
    }

    /// <summary>
    /// Register dynamic permission-based authorization.
    /// </summary>
    private static IServiceCollection AddPermissions(this IServiceCollection services) =>
        services
            .AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider, Permissions.PermissionPolicyProvider>()
            .AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, Permissions.PermissionAuthorizationHandler>();


    internal static IApplicationBuilder UseCurrentUser(this IApplicationBuilder app) =>
        app.UseMiddleware<CurrentUserMiddleware>();

    internal static IApplicationBuilder UseAuth(this IApplicationBuilder app) =>
        app
            .UseAuthentication()
            .UseCurrentUser()
            .UseAuthorization();
}
