using System.Security.Claims;
using System.Text.Encodings.Web;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Boilerate.Tests;

public sealed class CurrentUserTests
{
    [Fact]
    public void ReaderAndInitializerShareTheSameScopedInstance()
    {
        var services = new ServiceCollection();
        AddAuthentication(services);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var reader = firstScope.ServiceProvider.GetRequiredService<ICurrentUser>();
        var initializer = firstScope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>();
        Assert.Same(reader, initializer);
        Assert.NotSame(reader, secondScope.ServiceProvider.GetRequiredService<ICurrentUser>());

        initializer.SetCurrentUser(CreatePrincipal(TestAuthenticationHandler.UserId));
        Assert.Equal(TestAuthenticationHandler.UserId, reader.GetUserId());
        Assert.False(secondScope.ServiceProvider.GetRequiredService<ICurrentUser>().IsAuthenticated());
    }

    [Fact]
    public async Task MiddlewareCapturesTheAuthenticatedPrincipal()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        AddAuthentication(builder.Services);
        builder.Services.AddAuthorization(options => options.FallbackPolicy = null);
        builder.Services.RemoveAll<IAuthorizationHandler>();
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = "Test";
            options.DefaultChallengeScheme = "Test";
        }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
        await using WebApplication app = builder.Build();
        Boilerate.Infrastructure.Auth.Startup.UseAuth(app);
        app.Run(context => context.Response.WriteAsync(context.RequestServices.GetRequiredService<ICurrentUser>().GetUserId().ToString()));
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();

        Assert.Equal(TestAuthenticationHandler.UserId.ToString(), await client.GetStringAsync("/"));
    }

    [Fact]
    public void BackgroundActorDoesNotBecomeAuthenticated()
    {
        var currentUser = new CurrentUser();
        currentUser.SetCurrentUserId(TestAuthenticationHandler.UserId.ToString());

        Assert.Equal(TestAuthenticationHandler.UserId, currentUser.GetUserId());
        Assert.False(currentUser.IsAuthenticated());
        Assert.False(currentUser.IsInRole("Admin"));
    }

    [Fact]
    public void InitializationCannotSwitchFromBackgroundActorToPrincipal()
    {
        var currentUser = new CurrentUser();
        currentUser.SetCurrentUserId(TestAuthenticationHandler.UserId.ToString());

        Assert.Throws<InvalidOperationException>(() => currentUser.SetCurrentUser(CreatePrincipal(Guid.NewGuid())));
    }

    private static void AddAuthentication(IServiceCollection services)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SecuritySettings:JwtSettings:Key"] = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
        }).Build();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        Boilerate.Infrastructure.Auth.Startup.AddAuth(services, configuration);
    }

    private static ClaimsPrincipal CreatePrincipal(Guid userId) => new(new ClaimsIdentity(
        new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "Test"));

    private sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public static readonly Guid UserId = Guid.Parse("768f6b78-ce3d-44e1-8417-1a12b206ac42");

        public TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(
            AuthenticateResult.Success(new AuthenticationTicket(CreatePrincipal(UserId), Scheme.Name)));
    }
}