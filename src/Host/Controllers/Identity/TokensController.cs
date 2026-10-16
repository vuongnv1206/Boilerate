using Boilerate.Application.Identity.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.RateLimiting;

namespace Boilerate.Host.Controllers.Identity;

/// <summary>
/// Tokens Controller - Authentication endpoints
/// Endpoints: Login, Refresh token
/// </summary>
public sealed class TokensController : BaseApiController
{
    private readonly ITokenService _tokenService;

    public TokensController(ITokenService tokenService)
    {
        _tokenService = tokenService;
    }

    /// <summary>
    /// Login endpoint - Đăng nhập bằng email và password
    /// </summary>
    /// <param name="request">Token request với email và password</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JWT token và refresh token</returns>
    [HttpPost("get")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [OpenApiOperation("Request an access token using credentials.", "")]
    public Task<TokenResponse> GetTokenAsync(
    TokenRequest request,
        CancellationToken cancellationToken)
    {
        // Get client IP address
        var ipAddress = GetIpAddress();

        // Validate credentials và generate JWT token
        return _tokenService.GetTokenAsync(request, ipAddress!, cancellationToken);
    }

    /// <summary>
    /// Refresh token endpoint - Làm mới access token bằng refresh token
    /// </summary>
    /// <param name="request">Refresh token request</param>
    /// <returns>New JWT token và refresh token</returns>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [OpenApiOperation("Request an access token using a refresh token.", "")]
    public Task<TokenResponse> RefreshAsync(RefreshTokenRequest request)
    {
        // Get client IP address
        var ipAddress = GetIpAddress();

        // Validate refresh token và generate new JWT token
        return _tokenService.RefreshTokenAsync(request, ipAddress!);
    }

    [HttpGet("sessions")]
    public Task<List<SessionDto>> Sessions(CancellationToken ct) => _tokenService.GetSessionsAsync(User.GetUserId()!, ct);
    [HttpDelete("sessions/{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    { await _tokenService.RevokeSessionAsync(User.GetUserId()!, id, ct); return NoContent(); }
    [HttpDelete("sessions")]
    public async Task<IActionResult> RevokeAll(CancellationToken ct)
    { await _tokenService.RevokeAllAsync(User.GetUserId()!, ct); return NoContent(); }
    private string GetIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
