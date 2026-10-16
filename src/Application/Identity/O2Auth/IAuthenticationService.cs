using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Identity.Tokens;

namespace Boilerate.Application.Identity.O2Auth;

/// <summary>
/// Authentication service cho OAuth2 social login.
/// </summary>
public interface IAuthenticationService : ITransientService
{
    Task LinkGoogleAsync(string idToken, string userId);
    Task LinkFacebookAsync(string accessToken, string userId);
    /// <summary>
    /// Đăng nhập bằng Google ID Token.
    /// ID Token Flow: Frontend validate với Google và gửi ID Token cho API.
    /// </summary>
    /// <param name="idToken">Google ID Token từ frontend.</param>
    /// <param name="ipAddress">IP address của user.</param>
    /// <returns>JWT token response.</returns>
    Task<TokenResponse> GoogleSignIn(string idToken, string ipAddress);

    /// <summary>
    /// Đăng nhập bằng Google Authorization Code.
    /// Authorization Code Flow: API trao đổi code để lấy token từ Google.
    /// </summary>
    /// <param name="authorizedCode">Authorization code từ Google.</param>
    /// <param name="ipAddress">IP address của user.</param>
    /// <returns>JWT token response.</returns>
    Task<TokenResponse> GoogleSignIn2(string authorizedCode, string ipAddress);

    /// <summary>
    /// Đăng nhập bằng Facebook Access Token.
    /// </summary>
    /// <param name="accessToken">Facebook Access Token từ frontend.</param>
    /// <param name="ipAddress">IP address của user.</param>
    /// <returns>JWT token response.</returns>
    Task<TokenResponse> FacebookSignIn(string accessToken, string ipAddress);
}
