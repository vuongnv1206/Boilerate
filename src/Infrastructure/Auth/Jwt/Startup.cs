using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Boilerate.Infrastructure.Auth.Jwt;

/// <summary>
/// JWT authentication startup configuration
/// </summary>
internal static class Startup
{
    /// <summary>
    /// Add JWT authentication services
    /// </summary>
    internal static IServiceCollection AddJwtAuth(this IServiceCollection services)
    {
        // Bind JwtSettings từ configuration
        services.AddOptions<JwtSettings>()
            .BindConfiguration($"SecuritySettings:{nameof(JwtSettings)}")
            .ValidateDataAnnotations() // Validate với IValidatableObject
            .ValidateOnStart(); // Validate khi app start (fail fast)

        // Register ConfigureJwtBearerOptions
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();

        // Add JWT Bearer authentication
        return services
            .AddAuthentication(authentication =>
            {
                authentication.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                authentication.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, null!) // Configure bởi ConfigureJwtBearerOptions
            .Services;
    }
}
