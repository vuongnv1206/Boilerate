namespace Boilerate.Infrastructure.Auth.OAuth2;

/// <summary>
/// Google OAuth2 authentication settings.
/// Lấy từ appsettings.json section "Authentication:Google".
/// </summary>
public class GoogleAuthSettings
{
    public string RedirectUri { get; set; } = "postmessage";
    /// <summary>
    /// Section name trong appsettings.json.
    /// </summary>
    public const string SectionName = "Authentication:Google";

    /// <summary>
    /// Gets or sets Google OAuth2 Client ID.
    /// Lấy từ Google Cloud Console.
    /// </summary>
    public string ClientId { get; set; } = default!;

    /// <summary>
    /// Gets or sets Google OAuth2 Client Secret.
    /// Lấy từ Google Cloud Console.
    /// </summary>
    public string ClientSecret { get; set; } = default!;
}
