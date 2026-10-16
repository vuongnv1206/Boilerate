using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Identity.O2Auth;
using Boilerate.Application.Identity.Tokens;
using Boilerate.Domain.Identity;
using Boilerate.Host.Configurations;
using Boilerate.Host.Controllers.Identity;
using Boilerate.Infrastructure;
using Boilerate.Infrastructure.Auth;
using Boilerate.Infrastructure.Auth.OAuth2;
using Boilerate.Infrastructure.Identity;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class TokenBoundaryIntegrationTests
{
    private const string Password = "BoundaryPassword!298745";
    private static async Task<ApplicationUser> User(FoundationDatabase fixture)
    {
        using var scope = fixture.Scope();
        var user = new ApplicationUser { UserName = "boundary", Email = "boundary@example.test", IsActive = true, EmailConfirmed = true };
        Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user, Password)).Succeeded);
        return user;
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task JwtRequiresIssuerAudienceSessionAndQueryTokenIsConfinedToHub(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, notifications: true); await fixture.CreateAsync();
        var user = await User(fixture);
        TokenResponse response;
        using (var scope = fixture.Scope()) response = await scope.ServiceProvider.GetRequiredService<ITokenService>().GetTokenAsync(new(user.Email!, Password), "127.0.0.1", default);
        await using var server = await TestHostApplication.StartAsync(fixture, false);
        using var client = server.GetTestClient();
        foreach (var invalid in new[] { "issuer", "audience", "session" })
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.Id) };
            if (invalid != "session") claims.Add(new("sid", response.SessionId.ToString()));
            var jwt = new JwtSecurityToken(invalid == "issuer" ? "wrong" : "Boilerate", invalid == "audience" ? "wrong" : "Boilerate.Api", claims,
                expires: DateTime.UtcNow.AddMinutes(1), signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(fixture.Configuration["SecuritySettings:JwtSettings:Key"]!)), SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tokens/sessions")).StatusCode);
        }
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/tokens/sessions?access_token=" + Uri.EscapeDataString(response.Token))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/hubs/notifications/negotiate?negotiateVersion=1", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/hubs/notifications/negotiate?negotiateVersion=1&access_token=" + Uri.EscapeDataString(response.Token), null)).StatusCode);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task PasswordLockoutIsPersistedAndInactiveAccountsCannotRefresh(string provider)
    {
        await using var fixture = new FoundationDatabase(provider); await fixture.CreateAsync(); var user = await User(fixture);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var scope = fixture.Scope();
            await Assert.ThrowsAsync<UnauthorizedException>(() => scope.ServiceProvider.GetRequiredService<ITokenService>().GetTokenAsync(new(user.Email!, "incorrect"), "127.0.0.1", default));
        }
        using (var scope = fixture.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.True((await db.Users.SingleAsync()).LockoutEnd > DateTimeOffset.UtcNow);
            await Assert.ThrowsAsync<UnauthorizedException>(() => scope.ServiceProvider.GetRequiredService<ITokenService>().GetTokenAsync(new(user.Email!, Password), "127.0.0.1", default));
            await db.Users.ExecuteUpdateAsync(s => s.SetProperty(u => u.LockoutEnd, (DateTimeOffset?)null).SetProperty(u => u.AccessFailedCount, 0));
        }
        TokenResponse token;
        using (var scope = fixture.Scope()) token = await scope.ServiceProvider.GetRequiredService<ITokenService>().GetTokenAsync(new(user.Email!, Password), "127.0.0.1", default);
        using (var scope = fixture.Scope()) await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
        using (var scope = fixture.Scope()) await Assert.ThrowsAsync<UnauthorizedException>(() => scope.ServiceProvider.GetRequiredService<ITokenService>().RefreshTokenAsync(new() { RefreshToken = token.RefreshToken }, "127.0.0.1"));
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task FacebookChecksAppDoesNotLinkByEmailAndRejectsInactiveLinkedUser(string provider)
    {
        await using var fixture = new FoundationDatabase(provider); await fixture.CreateAsync(); var user = await User(fixture);
        using var scope = fixture.Scope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        IAuthenticationService Service(string appId)
        {
            var factory = Substitute.For<IHttpClientFactory>();
            factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(new FacebookHandler(appId)));
            return new AuthenticationService(manager, Options.Create(new GoogleAuthSettings()),
                Options.Create(new FacebookAuthSettings { AppId = "expected", AppSecret = "isolated-test-secret" }), Options.Create(new SecuritySettings()),
                scope.ServiceProvider.GetRequiredService<ITokenService>(), factory, scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }
        await Assert.ThrowsAsync<UnauthorizedException>(() => Service("wrong").FacebookSignIn("opaque-test-token", "127.0.0.1"));
        await Assert.ThrowsAsync<ConflictException>(() => Service("expected").FacebookSignIn("opaque-test-token", "127.0.0.1"));
        Assert.Null(await manager.FindByLoginAsync("Facebook", "external-subject"));
        await Service("expected").LinkFacebookAsync("opaque-test-token", user.Id);
        var tracked = (await manager.FindByIdAsync(user.Id))!; tracked.IsActive = false;
        Assert.True((await manager.UpdateAsync(tracked)).Succeeded);
        await Assert.ThrowsAsync<UnauthorizedException>(() => Service("expected").FacebookSignIn("opaque-test-token", "127.0.0.1"));
    }

    private sealed class FacebookHandler(string appId) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(request.RequestUri!.AbsolutePath.EndsWith("debug_token", StringComparison.Ordinal)
                    ? (object)new { data = new { is_valid = true, app_id = appId, user_id = "external-subject" } }
                    : new { id = "external-subject", email = "boundary@example.test", first_name = "Test", last_name = "User" })
            });
    }
}
