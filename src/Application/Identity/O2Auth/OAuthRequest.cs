namespace Boilerate.Application.Identity.O2Auth;

/// <summary>
/// OAuth request model.
/// </summary>
public class OAuthRequest
{
    /// <summary>
    /// Gets or sets ID Token từ OAuth provider (Google/Facebook).
    /// </summary>
    public string IdToken { get; set; } = default!;
}
