namespace Boilerate.Infrastructure.Auth;

/// <summary>
/// Security configuration settings
/// </summary>
public class SecuritySettings
{
    public bool PasswordRecoveryEnabled { get; set; }
    public bool AllowSelfRegistration { get; set; }
    public string PublicBaseUrl { get; set; } = "https://localhost:7001";
    /// <summary>
    /// Authentication provider (e.g., "Jwt", "AzureAd")
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Require email confirmation before login
    /// </summary>
    public bool RequireConfirmedAccount { get; set; }
}
