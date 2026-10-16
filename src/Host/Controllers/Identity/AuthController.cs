using Microsoft.AspNetCore.RateLimiting;
using Boilerate.Application.Identity.O2Auth;
using Boilerate.Host.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;
using Boilerate.Shared.Authorization;

namespace Boilerate.Host.Controllers.Identity;

/// <summary>
/// Authentication controller cho OAuth2 social login.
/// </summary>
public class AuthController : BaseApiController
{
    private readonly IAuthenticationService _authenticationService;

    public AuthController(IAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
    }

    /// <summary>
    /// Đăng nhập bằng Google ID Token.
    /// </summary>
    /// <param name="request">Request chứa ID Token.</param>
    /// <returns>Token response.</returns>
    [HttpPost("google")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [OpenApiOperation("Đăng nhập bằng Google ID Token", "")]
    public async Task<IActionResult> GoogleLogin([FromBody] OAuthRequest request)
    {
        var response = await _authenticationService.GoogleSignIn(request.IdToken, GetIpAddress()!);
        return Ok(response);
    }

    /// <summary>
    /// Đăng nhập bằng Google Authorization Code.
    /// </summary>
    /// <param name="authorizedCode">Code nhận từ Google.</param>
    /// <returns>Token response.</returns>
    [HttpPost("google/code")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [OpenApiOperation("Đăng nhập bằng Google Authorization Code", "")]
    public async Task<IActionResult> GoogleLogin2([FromBody] string authorizedCode)
    {
        var response = await _authenticationService.GoogleSignIn2(authorizedCode, GetIpAddress()!);
        return Ok(response);
    }

    /// <summary>
    /// Đăng nhập bằng Facebook Access Token.
    /// </summary>
    /// <param name="request">Request chứa Access Token.</param>
    /// <returns>Token response.</returns>
    [HttpPost("facebook")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [OpenApiOperation("Đăng nhập bằng Facebook Access Token", "")]
    public async Task<IActionResult> FacebookLogin([FromBody] OAuthRequest request)
    {
        var response = await _authenticationService.FacebookSignIn(request.IdToken, GetIpAddress()!);
        return Ok(response);
    }

    [HttpPost("google/link")]
    public async Task<IActionResult> LinkGoogle(OAuthRequest request)
    { await _authenticationService.LinkGoogleAsync(request.IdToken, User.GetUserId()!); return NoContent(); }
    [HttpPost("facebook/link")]
    public async Task<IActionResult> LinkFacebook(OAuthRequest request)
    { await _authenticationService.LinkFacebookAsync(request.IdToken, User.GetUserId()!); return NoContent(); }
    private string GetIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
