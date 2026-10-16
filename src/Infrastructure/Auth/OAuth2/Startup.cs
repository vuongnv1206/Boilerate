using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerate.Infrastructure.Auth.OAuth2;

internal static class Startup
{
    /// <summary>
    /// Thêm OAuth2 authentication providers (Google, Facebook).
    /// </summary>
    internal static IServiceCollection AddO2Authentication(this IServiceCollection services, IConfiguration configuration)
    {
        // Đăng ký GoogleAuthSettings từ configuration
        services.Configure<GoogleAuthSettings>(configuration.GetSection(GoogleAuthSettings.SectionName));

        // Đăng ký FacebookAuthSettings từ configuration
        services.Configure<FacebookAuthSettings>(configuration.GetSection(FacebookAuthSettings.SectionName));

        var googleSettings = configuration.GetSection(GoogleAuthSettings.SectionName).Get<GoogleAuthSettings>();
        var facebookSettings = configuration.GetSection(FacebookAuthSettings.SectionName).Get<FacebookAuthSettings>();

        // Thêm authentication schemes
        var authBuilder = services.AddAuthentication();

        if (googleSettings is { ClientId: { Length: > 0 }, ClientSecret: { Length: > 0 } })
        {
            authBuilder.AddGoogle(googleOptions =>
            {
                googleOptions.ClientId = googleSettings.ClientId;
                googleOptions.ClientSecret = googleSettings.ClientSecret;
                googleOptions.SaveTokens = true;
            });
        }

        if (facebookSettings is { AppId: { Length: > 0 }, AppSecret: { Length: > 0 } })
        {
            authBuilder.AddFacebook(facebookOptions =>
            {
                facebookOptions.AppId = facebookSettings.AppId;
                facebookOptions.AppSecret = facebookSettings.AppSecret;
                facebookOptions.SaveTokens = true;
            });
        }

        return services;
    }
}
