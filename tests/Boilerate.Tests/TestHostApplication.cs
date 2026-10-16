using Boilerate.Host.Configurations;
using Boilerate.Host.Controllers.Identity;
using Boilerate.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerate.Tests;

internal static class TestHostApplication
{
    internal static async Task<WebApplication> StartAsync(FoundationDatabase fixture, bool catalog, bool notifications = true)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Production",
            ApplicationName = typeof(TokensController).Assembly.GetName().Name
        });
        builder.WebHost.UseTestServer();
        fixture.ConfigureServices(builder.Services);
        builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("authentication", limiter =>
        {
            limiter.PermitLimit = 100;
            limiter.Window = TimeSpan.FromMinutes(1);
        }));
        builder.Services.AddControllers().AddApplicationPart(typeof(TokensController).Assembly)
            .ConfigureApplicationPartManager(parts => parts.FeatureProviders.Add(new CatalogControllerFeatureProvider(catalog, notifications)));
        WebApplication app = builder.Build();
        app.UseInfrastructure(fixture.Configuration);
        app.MapEndpoints();
        try
        {
            await app.StartAsync();
            return app;
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }
}
