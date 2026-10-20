using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Identity.Tokens;
using Boilerate.Domain.Identity;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using NSubstitute;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class BrowserSessionTests
{
    private const string CookieName = "boilerate.refresh";
    private const string Password = "BrowserPassword!298745";
    private const string Email = "browser@example.test";
    private static readonly TokenRequest Credentials = new(Email, Password);

    [Theory]
    [InlineData("Production", "http://localhost", true)]
    [InlineData("Development", "http://localhost", false)]
    [InlineData("Development", "https://localhost", true)]
    public async Task LoginSetsProtectedCookieAndReturnsOnlyAccessMetadata(string environment, string origin, bool secure)
    {
        await using var fixture = new FoundationDatabase("mssql");
        var tokens = Substitute.For<ITokenService>();
        var response = Token();
        tokens.GetTokenAsync(Arg.Any<TokenRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(response);
        await using var server = await TestHostApplication.StartAsync(fixture, false, configureServices: services => ReplaceTokens(services, tokens), environment: environment);
        using var client = BrowserClient(server.GetTestClient(), origin);

        var result = await client.PostAsJsonAsync("/api/tokens/session", Credentials);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        AssertNoStore(result);
        var cookie = RefreshCookie(result);
        Assert.True(cookie.HttpOnly);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Strict, cookie.SameSite);
        Assert.Equal(secure, cookie.Secure);
        Assert.Equal("/api/tokens/session", cookie.Path.Value);
        Assert.False(cookie.Domain.HasValue);
        Assert.Equal(new DateTimeOffset(response.RefreshTokenExpiryTime), cookie.Expires);
        Assert.Equal(response.RefreshToken, Uri.UnescapeDataString(cookie.Value.Value!));
        var body = await result.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(["accessTokenExpiresAt", "sessionId", "token", "userId"], body.EnumerateObject().Select(property => property.Name).Order().ToArray());
        Assert.Equal(response.UserId, body.GetProperty("userId").GetString());
        Assert.DoesNotContain(response.RefreshToken, await result.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("POST", "/api/tokens/session")]
    [InlineData("POST", "/api/tokens/session/refresh")]
    [InlineData("DELETE", "/api/tokens/session")]
    public async Task SessionActionsRequireBrowserHeaderBeforeMutatingState(string method, string path)
    {
        await using var fixture = new FoundationDatabase("mssql");
        var tokens = Substitute.For<ITokenService>();
        await using var server = await TestHostApplication.StartAsync(fixture, false, configureServices: services => ReplaceTokens(services, tokens));
        using var client = server.GetTestClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Add("Cookie", CookieName + "=existing-refresh");
        if (path == "/api/tokens/session" && method == "POST") request.Content = JsonContent.Create(Credentials);

        var result = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
        AssertNoStore(result);
        Assert.False(result.Headers.Contains("Set-Cookie"));
        Assert.Empty(tokens.ReceivedCalls());
    }

    [Fact]
    public async Task RefreshWithoutCookieReturnsUnauthorizedAndDeletesCookie()
    {
        await using var fixture = new FoundationDatabase("mssql");
        var tokens = Substitute.For<ITokenService>();
        await using var server = await TestHostApplication.StartAsync(fixture, false, configureServices: services => ReplaceTokens(services, tokens));
        using var client = BrowserClient(server.GetTestClient());

        var result = await client.PostAsync("/api/tokens/session/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        AssertNoStore(result);
        AssertDeletedCookie(result);
        Assert.Empty(tokens.ReceivedCalls());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RefreshDeletesRejectedCookieButPreservesItOnTransientFailure(bool unauthorized)
    {
        await using var fixture = new FoundationDatabase("mssql");
        var tokens = Substitute.For<ITokenService>();
        Exception failure = unauthorized ? new UnauthorizedException("Refresh expired.") : new InvalidOperationException("Database unavailable.");
        tokens.RefreshTokenAsync(Arg.Any<RefreshTokenRequest>(), Arg.Any<string>()).Returns(Task.FromException<TokenResponse>(failure));
        await using var server = await TestHostApplication.StartAsync(fixture, false, configureServices: services => ReplaceTokens(services, tokens));
        using var client = BrowserClient(server.GetTestClient());
        client.DefaultRequestHeaders.Add("Cookie", CookieName + "=test-refresh");

        var result = await client.PostAsync("/api/tokens/session/refresh", null);

        Assert.Equal(unauthorized ? HttpStatusCode.Unauthorized : HttpStatusCode.InternalServerError, result.StatusCode);
        AssertNoStore(result);
        if (unauthorized) AssertDeletedCookie(result);
        else Assert.False(result.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task LogoutWithoutCookieIsIdempotent()
    {
        await using var fixture = new FoundationDatabase("mssql");
        var tokens = Substitute.For<ITokenService>();
        await using var server = await TestHostApplication.StartAsync(fixture, false, configureServices: services => ReplaceTokens(services, tokens));
        using var client = BrowserClient(server.GetTestClient());
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var result = await client.DeleteAsync("/api/tokens/session");
            Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
            AssertNoStore(result);
            AssertDeletedCookie(result);
        }
        Assert.Empty(tokens.ReceivedCalls());
    }

    [Fact]
    public async Task ExistingJsonTokenRoutesKeepTheirResponseContract()
    {
        await using var fixture = new FoundationDatabase("mssql");
        var tokens = Substitute.For<ITokenService>();
        var expected = Token();
        tokens.GetTokenAsync(Arg.Any<TokenRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(expected);
        tokens.RefreshTokenAsync(Arg.Any<RefreshTokenRequest>(), Arg.Any<string>()).Returns(expected);
        await using var server = await TestHostApplication.StartAsync(fixture, false, configureServices: services => ReplaceTokens(services, tokens));
        using var client = server.GetTestClient();
        foreach (var request in new[]
        {
            (Path: "/api/tokens/get", Body: (object)Credentials),
            (Path: "/api/tokens/refresh", Body: new RefreshTokenRequest { RefreshToken = "legacy-refresh" })
        })
        {
            var result = await client.PostAsJsonAsync(request.Path, request.Body);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var json = await result.Content.ReadAsStringAsync();
            Assert.Equal(expected with { UserId = string.Empty }, JsonSerializer.Deserialize<TokenResponse>(json, JsonSerializerOptions.Web));
            using var document = JsonDocument.Parse(json);
            var body = document.RootElement;
            Assert.False(body.TryGetProperty("userId", out _));
            Assert.False(result.Headers.Contains("Set-Cookie"));
        }
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ReloadRefreshesSessionAndLogoutRevokesItsFamilyWithExpiredAccess(string provider)
    {
        await using var fixture = new FoundationDatabase(provider);
        await fixture.CreateAsync();
        var userId = await CreateUser(fixture);
        await using var server = await TestHostApplication.StartAsync(fixture, false);
        using var client = BrowserClient(server.GetTestClient());
        var login = await client.PostAsJsonAsync("/api/tokens/session", Credentials);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var initial = (await login.Content.ReadFromJsonAsync<BrowserSessionResponse>())!;
        Assert.Equal(userId, initial.UserId);
        var initialCookie = CookieHeader(login);
        // Reload has no access token. The browser sends only its HttpOnly cookie.
        client.DefaultRequestHeaders.Add("Cookie", initialCookie);
        var refresh = await client.PostAsync("/api/tokens/session/refresh", null);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var restored = (await refresh.Content.ReadFromJsonAsync<BrowserSessionResponse>())!;
        Assert.Equal(userId, restored.UserId);
        Assert.NotEqual(initial.SessionId, restored.SessionId);
        Assert.NotEqual(initialCookie, CookieHeader(refresh));
        using var accessClient = server.GetTestClient();
        accessClient.DefaultRequestHeaders.Authorization = new("Bearer", restored.Token);
        Assert.Equal(HttpStatusCode.OK, (await accessClient.GetAsync("/api/tokens/sessions")).StatusCode);
        using var otherClient = server.GetTestClient();
        var otherLogin = await otherClient.PostAsJsonAsync("/api/tokens/get", Credentials);
        var other = (await otherLogin.Content.ReadFromJsonAsync<TokenResponse>())!;
        otherClient.DefaultRequestHeaders.Authorization = new("Bearer", other.Token);

        // An expired access token and even an already-consumed refresh cookie can still sign out the family.
        client.DefaultRequestHeaders.Authorization = new("Bearer", ExpiredToken(initial.Token, fixture));
        var logout = await client.DeleteAsync("/api/tokens/session");

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        AssertDeletedCookie(logout);
        Assert.Equal(HttpStatusCode.Unauthorized, (await accessClient.GetAsync("/api/tokens/sessions")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await otherClient.GetAsync("/api/tokens/sessions")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/tokens/session")).StatusCode);
        using var scope = fixture.Scope();
        var sessions = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().UserSessions.ToListAsync();
        Assert.NotNull(sessions.Single(session => session.Id == initial.SessionId).RevokedOn);
        Assert.NotNull(sessions.Single(session => session.Id == restored.SessionId).RevokedOn);
        Assert.Null(sessions.Single(session => session.Id == other.SessionId).RevokedOn);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ReusedRefreshCookieIsClearedAndRevokesRotatedAccess(string provider)
    {
        await using var fixture = new FoundationDatabase(provider);
        await fixture.CreateAsync();
        await CreateUser(fixture);
        await using var server = await TestHostApplication.StartAsync(fixture, false);
        using var client = BrowserClient(server.GetTestClient());
        var login = await client.PostAsJsonAsync("/api/tokens/session", Credentials);
        client.DefaultRequestHeaders.Add("Cookie", CookieHeader(login));
        var firstRefresh = await client.PostAsync("/api/tokens/session/refresh", null);
        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);
        var rotated = (await firstRefresh.Content.ReadFromJsonAsync<BrowserSessionResponse>())!;

        var replay = await client.PostAsync("/api/tokens/session/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        AssertDeletedCookie(replay);
        client.DefaultRequestHeaders.Authorization = new("Bearer", rotated.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tokens/sessions")).StatusCode);
    }

    private static TokenResponse Token() => new("test-access", "opaque-refresh+/=", new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc), Guid.NewGuid(), DateTime.UtcNow.AddMinutes(15))
    {
        UserId = "browser-user"
    };

    private static void ReplaceTokens(IServiceCollection services, ITokenService tokens)
    {
        services.RemoveAll<ITokenService>();
        services.AddSingleton(tokens);
    }

    private static HttpClient BrowserClient(HttpClient client, string origin = "https://localhost")
    {
        client.BaseAddress = new Uri(origin);
        client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        return client;
    }

    private static SetCookieHeaderValue RefreshCookie(HttpResponseMessage response) => SetCookieHeaderValue.ParseList(response.Headers.GetValues("Set-Cookie").ToList())
        .Single(cookie => cookie.Name == CookieName);

    private static string CookieHeader(HttpResponseMessage response)
    {
        var cookie = RefreshCookie(response);
        return cookie.Name + "=" + cookie.Value;
    }

    private static void AssertNoStore(HttpResponseMessage response) => Assert.True(response.Headers.CacheControl?.NoStore);

    private static void AssertDeletedCookie(HttpResponseMessage response)
    {
        var cookie = RefreshCookie(response);
        Assert.True(cookie.Expires < DateTimeOffset.UtcNow);
        Assert.Equal("/api/tokens/session", cookie.Path.Value);
        Assert.True(cookie.HttpOnly);
    }

    private static async Task<string> CreateUser(FoundationDatabase fixture)
    {
        using var scope = fixture.Scope();
        var user = new ApplicationUser { UserName = "browser", Email = Email, IsActive = true, EmailConfirmed = true };
        Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user, Password)).Succeeded);
        return user.Id;
    }

    private static string ExpiredToken(string token, FoundationDatabase fixture)
    {
        var current = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var expired = new JwtSecurityToken(current.Issuer, current.Audiences.Single(), current.Claims,
            DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(fixture.Configuration["SecuritySettings:JwtSettings:Key"]!)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(expired);
    }
}
