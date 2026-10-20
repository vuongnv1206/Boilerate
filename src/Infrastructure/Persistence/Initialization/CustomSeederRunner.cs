using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Boilerate.Infrastructure.Persistence.Initialization;

/// <summary>Runs scoped seeders sequentially; never migrates, bootstraps Identity or starts HTTP.</summary>
public sealed class CustomSeederRunner(
    IEnumerable<ICustomSeeder> seeders,
    IHostEnvironment environment,
    ILogger<CustomSeederRunner> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("--seed-dev is available only in the Development environment.");
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var seeder in seeders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogInformation("Running development seeder {SeederName}", seeder.GetType().Name);
            await seeder.InitializeAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogInformation("Development seeder {SeederName} completed", seeder.GetType().Name);
        }
        logger.LogInformation("Development seeding completed");
    }
}

internal static class DevelopmentSeederRegistration
{
    internal static IServiceCollection AddDevelopmentSeeders(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<CustomSeederRunner>();
        if (configuration.GetValue<bool>("Modules:Catalog:Enabled"))
            services.AddScoped<ICustomSeeder, CatalogDevelopmentSeeder>();
        return services;
    }
}
