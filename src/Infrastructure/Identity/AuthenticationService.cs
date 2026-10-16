using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Identity.O2Auth;
using Boilerate.Application.Identity.Tokens;
using Boilerate.Domain.Identity;
using Boilerate.Infrastructure.Auth;
using Boilerate.Infrastructure.Auth.OAuth2;
using Boilerate.Infrastructure.Persistence.Context;
using Google.Apis.Auth;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
namespace Boilerate.Infrastructure.Identity;
public sealed class AuthenticationService : IAuthenticationService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly GoogleAuthSettings _google;
    private readonly FacebookAuthSettings _facebook;
    private readonly SecuritySettings _security;
    private readonly ITokenService _tokens;
    private readonly IHttpClientFactory _http;
    private readonly ApplicationDbContext _db;
    public AuthenticationService(UserManager<ApplicationUser> users, IOptions<GoogleAuthSettings> google,
        IOptions<FacebookAuthSettings> facebook, IOptions<SecuritySettings> security, ITokenService tokens, IHttpClientFactory http, ApplicationDbContext db)
        => (_users, _google, _facebook, _security, _tokens, _http, _db) = (users, google.Value, facebook.Value, security.Value, tokens, http, db);
    private async Task<GoogleJsonWebSignature.Payload> GooglePayload(string token)
    {
        if (string.IsNullOrWhiteSpace(_google.ClientId)) throw new ForbiddenException("Google login is not configured.");
        if (string.IsNullOrWhiteSpace(token)) throw new UnauthorizedException("Invalid Google token.");
        try { return await GoogleJsonWebSignature.ValidateAsync(token, new() { Audience = new[] { _google.ClientId } }); }
        catch (InvalidJwtException) { throw new UnauthorizedException("Invalid Google token."); }
    }
    public async Task<TokenResponse> GoogleSignIn(string token, string ipAddress)
    {
        var payload = await GooglePayload(token);
        var user = await ExternalUser("Google", payload.Subject, payload.Email, payload.EmailVerified, payload.GivenName, payload.FamilyName, payload.Picture);
        return await _tokens.GenerateTokensAndUpdateUser(user, ipAddress);
    }
    public async Task<TokenResponse> GoogleSignIn2(string authorizationCode, string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(_google.ClientId) || string.IsNullOrWhiteSpace(_google.ClientSecret))
            throw new ForbiddenException("Google authorization-code login is not configured.");
        using var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets { ClientId = _google.ClientId, ClientSecret = _google.ClientSecret },
            Scopes = new[] { "openid", "email", "profile" }
        });
        var response = await flow.ExchangeCodeForTokenAsync("me", authorizationCode, _google.RedirectUri, default);
        // Validate the ID token, including audience; userinfo alone is not an account-linking proof.
        return await GoogleSignIn(response.IdToken, ipAddress);
    }
    private async Task<FacebookUserInfo> FacebookPayload(string token)
    {
        if (string.IsNullOrWhiteSpace(_facebook.AppId) || string.IsNullOrWhiteSpace(_facebook.AppSecret))
            throw new ForbiddenException("Facebook login is not configured.");
        if (string.IsNullOrWhiteSpace(token)) throw new UnauthorizedException("Invalid Facebook token.");
        using var client = _http.CreateClient("OAuth");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _facebook.AppId + "|" + _facebook.AppSecret);
        using var debugResponse = await client.GetAsync("https://graph.facebook.com/debug_token?input_token=" + Uri.EscapeDataString(token));
        if (!debugResponse.IsSuccessStatusCode) throw new UnauthorizedException("Invalid Facebook token.");
        var debug = await debugResponse.Content.ReadFromJsonAsync<FacebookDebug>();
        if (debug?.Data is not { IsValid: true } data || data.AppId != _facebook.AppId || string.IsNullOrEmpty(data.UserId))
            throw new UnauthorizedException("Facebook token belongs to another app or is invalid.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var profileResponse = await client.GetAsync("https://graph.facebook.com/me?fields=id,email,first_name,last_name,picture");
        if (!profileResponse.IsSuccessStatusCode) throw new UnauthorizedException("Invalid Facebook token.");
        var profile = await profileResponse.Content.ReadFromJsonAsync<FacebookUserInfo>();
        if (profile is null || profile.Id != data.UserId || string.IsNullOrEmpty(profile.Email))
            throw new UnauthorizedException("Facebook identity or email is unavailable.");
        return profile;
    }
    public async Task<TokenResponse> FacebookSignIn(string accessToken, string ipAddress)
    {
        var payload = await FacebookPayload(accessToken);
        var user = await ExternalUser("Facebook", payload.Id, payload.Email, false, payload.FirstName, payload.LastName, payload.Picture?.Data?.Url);
        return await _tokens.GenerateTokensAndUpdateUser(user, ipAddress);
    }
    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new ConflictException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }
    private async Task<ApplicationUser> ExternalUser(string provider, string subject, string email, bool verified, string? first, string? last, string? image)
    {
        var linked = await _users.FindByLoginAsync(provider, subject);
        if (linked is not null) return linked;
        if (!System.Net.Mail.MailAddress.TryCreate(email, out _)) throw new UnauthorizedException("Invalid external email.");
        if (_security.RequireConfirmedAccount && !verified) throw new UnauthorizedException("A verified email is required.");
        if (await _users.FindByEmailAsync(email) is not null)
            throw new ConflictException("Sign in to the existing account and explicitly link this provider first.");
        await using var tx = await _db.Database.BeginTransactionAsync();
        var user = new ApplicationUser { Email = email, UserName = email, EmailConfirmed = verified,
            IsActive = true, FirstName = first, LastName = last, ImageUrl = image };
        Check(await _users.CreateAsync(user));
        Check(await _users.AddToRoleAsync(user, "Basic"));
        Check(await _users.AddLoginAsync(user, new UserLoginInfo(provider, subject, provider)));
        await tx.CommitAsync(); return user;
    }
    private async Task Link(string provider, string subject, string userId)
    {
        var user = await _users.FindByIdAsync(userId) ?? throw new UnauthorizedException("Account unavailable.");
        if (!user.IsActive) throw new UnauthorizedException("Account unavailable.");
        var existing = await _users.FindByLoginAsync(provider, subject);
        if (existing is not null && existing.Id != userId) throw new ConflictException("External login is already linked.");
        if (existing is null) Check(await _users.AddLoginAsync(user, new UserLoginInfo(provider, subject, provider)));
    }
    public async Task LinkGoogleAsync(string idToken, string userId) => await Link("Google", (await GooglePayload(idToken)).Subject, userId);
    public async Task LinkFacebookAsync(string accessToken, string userId) => await Link("Facebook", (await FacebookPayload(accessToken)).Id, userId);
}
public sealed class FacebookUserInfo
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("email")] public string Email { get; set; } = "";
    [JsonPropertyName("first_name")] public string? FirstName { get; set; }
    [JsonPropertyName("last_name")] public string? LastName { get; set; }
    [JsonPropertyName("picture")] public FacebookPicture? Picture { get; set; }
}
public sealed class FacebookPicture { [JsonPropertyName("data")] public FacebookPictureData? Data { get; set; } }
public sealed class FacebookPictureData { [JsonPropertyName("url")] public string? Url { get; set; } }
public sealed class FacebookDebug { [JsonPropertyName("data")] public FacebookDebugData? Data { get; set; } }
public sealed class FacebookDebugData
{
    [JsonPropertyName("is_valid")] public bool IsValid { get; set; }
    [JsonPropertyName("app_id")] public string AppId { get; set; } = "";
    [JsonPropertyName("user_id")] public string UserId { get; set; } = "";
}
