namespace Boilerate.Infrastructure.Auth.OAuth2;

/// <summary>
/// Facebook OAuth2 authentication settings.
/// Lấy từ appsettings.json section "Authentication:Facebook".
/// </summary>
public class FacebookAuthSettings
{
    /// <summary>
    /// Section name trong appsettings.json.
    /// </summary>
    public const string SectionName = "Authentication:Facebook";

    /// <summary>
    /// Gets or sets Facebook App ID.
    /// Lấy từ Facebook Developers Console.
    /// </summary>
    public string AppId { get; set; } = default!;

    /// <summary>
    /// Gets or sets Facebook App Secret.
    /// Lấy từ Facebook Developers Console.
    /// </summary>
    public string AppSecret { get; set; } = default!;
}
