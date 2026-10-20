using Boilerate.Application.Common.Interfaces;
using Boilerate.Domain.Identity;

namespace Boilerate.Application.Identity.Tokens;

/// <summary>
/// Service xử lý JWT token operations
/// </summary>
public interface ITokenService : ITransientService
{
    Task<List<SessionDto>> GetSessionsAsync(string userId, CancellationToken ct);
    Task RevokeSessionAsync(string userId, Guid sessionId, CancellationToken ct);
    Task RevokeAllAsync(string userId, CancellationToken ct);
    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct);
    /// <summary>
    /// Generate tokens khi user login (email + password)
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>TokenResponse chứa access token và refresh token</returns>
    Task<TokenResponse> GetTokenAsync(
        TokenRequest request,
        string ipAddress,
        CancellationToken cancellationToken);

    /// <summary>
    /// Refresh access token bằng refresh token
    /// </summary>
    /// <param name="request">Expired access token và refresh token</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <returns>TokenResponse chứa tokens mới</returns>
    Task<TokenResponse> RefreshTokenAsync(
        RefreshTokenRequest request,
        string ipAddress);

    /// <summary>
    /// Generate tokens và update user refresh token trong database
    /// </summary>
    /// <param name="user">ApplicationUser</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <returns>TokenResponse</returns>
    Task<TokenResponse> GenerateTokensAndUpdateUser(
        ApplicationUser user,
        string ipAddress);
}
