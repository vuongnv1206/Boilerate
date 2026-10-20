using Boilerate.Application.Identity.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Boilerate.Application.Common.Exceptions;

namespace Boilerate.Host.Controllers.Identity;

/// <summary>
/// Tokens Controller - Authentication endpoints
/// Endpoints: Login, Refresh token
/// </summary>
public sealed class TokensController : BaseApiController
{
    private const string RefreshCookie = "boilerate.refresh";
    private const string SessionPath = "/api/tokens/session";
    private readonly ITokenService _tokenService;
    private readonly IWebHostEnvironment _environment;

    public TokensController(ITokenService tokenService, IWebHostEnvironment environment)
    {
        _tokenService = tokenService;
        _environment = environment;
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

    [HttpPost("session")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    public async Task<BrowserSessionResponse> CreateSessionAsync(TokenRequest request, CancellationToken cancellationToken)
    {
        RequireBrowserRequest();
        var response = await _tokenService.GetTokenAsync(request, GetIpAddress(), cancellationToken);
        return SetBrowserSession(response);
    }

    [HttpPost("session/refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    public async Task<BrowserSessionResponse> RefreshSessionAsync()
    {
        RequireBrowserRequest();
        try
        {
            if (!Request.Cookies.TryGetValue(RefreshCookie, out var refreshToken) || string.IsNullOrWhiteSpace(refreshToken))
                throw new UnauthorizedException("Sign in again.");
            var response = await _tokenService.RefreshTokenAsync(new() { RefreshToken = refreshToken }, GetIpAddress());
            return SetBrowserSession(response);
        }
        catch (UnauthorizedException)
        {
            Response.Cookies.Delete(RefreshCookie, CookieOptions());
            throw;
        }
    }

    [HttpDelete("session")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    public async Task<IActionResult> DeleteSessionAsync(CancellationToken cancellationToken)
    {
        RequireBrowserRequest();
        if (Request.Cookies.TryGetValue(RefreshCookie, out var refreshToken))
            await _tokenService.RevokeRefreshTokenAsync(refreshToken, cancellationToken);
        Response.Cookies.Delete(RefreshCookie, CookieOptions());
        return NoContent();
    }

    private void RequireBrowserRequest()
    {
        Response.Headers.CacheControl = "no-store";
        if (Request.Headers["X-Requested-With"] != "XMLHttpRequest")
            throw new ForbiddenException("Browser session requests require X-Requested-With: XMLHttpRequest.");
    }

    private BrowserSessionResponse SetBrowserSession(TokenResponse response)
    {
        var options = CookieOptions();
        options.Expires = new DateTimeOffset(DateTime.SpecifyKind(response.RefreshTokenExpiryTime, DateTimeKind.Utc));
        Response.Cookies.Append(RefreshCookie, response.RefreshToken, options);
        return new(response.Token, response.SessionId, response.AccessTokenExpiresAt, response.UserId);
    }

    private CookieOptions CookieOptions() => new()
    {
        Path = SessionPath,
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,
        Secure = !_environment.IsDevelopment() || Request.IsHttps,
        IsEssential = true
    };

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
