using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Common.Contracts;
using Boilerate.Domain.Identity;
using Boilerate.Infrastructure.Persistence.Context;
using Boilerate.Infrastructure.Persistence.Initialization;
using Boilerate.Infrastructure.Persistence.Repository;
using Boilerate.Infrastructure.Persistence.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;

namespace Boilerate.Infrastructure.Persistence;

internal static class Startup
{
    private static readonly ILogger _logger = Log.ForContext(typeof(Startup));

    internal static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        services.AddOptions<DatabaseSettings>()
            .BindConfiguration(nameof(DatabaseSettings))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services
            .AddDbContext<ApplicationDbContext>((sp, options) =>
            {
                var dbSettings = sp.GetRequiredService<IOptions<DatabaseSettings>>().Value;
                _logger.Information("DB Provider: {provider}", dbSettings.DBProvider);
                options.UseDatabase(dbSettings.DBProvider, dbSettings.ConnectionString);
            })
            .AddRepositories();
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        // Register base repositories and related services
        services.AddScoped(typeof(IRepository<>), typeof(ApplicationDbRepository<>));
        services.AddScoped(typeof(IPermanentDeleteService<>), typeof(PermanentDeleteService<>));

        foreach (var aggregateRootType in
            typeof(IAggregateRoot).Assembly.GetExportedTypes()
                .Where(t => typeof(IAggregateRoot).IsAssignableFrom(t) && t.IsClass)
                .ToList())
        {
            // IReadRepository<T> -> alias of IRepository<T>
            services.AddScoped(
                typeof(IReadRepository<>).MakeGenericType(aggregateRootType),
                sp => sp.GetRequiredService(typeof(IRepository<>).MakeGenericType(aggregateRootType)));

            // IRepositoryWithEvents<T> -> EventAddingRepositoryDecorator wrapping IRepository
            services.AddScoped(
                typeof(IRepositoryWithEvents<>).MakeGenericType(aggregateRootType),
                sp => Activator.CreateInstance(
                    typeof(EventAddingRepositoryDecorator<>).MakeGenericType(aggregateRootType),
                    sp.GetRequiredService(typeof(IRepository<>).MakeGenericType(aggregateRootType)))
                ?? throw new InvalidOperationException($"Could not create EventAddingRepositoryDecorator for {aggregateRootType.Name}"));
        }


        return services;
    }



    internal static DbContextOptionsBuilder UseDatabase(
        this DbContextOptionsBuilder builder,
        string provider,
        string connectionString)
    {
        return provider.ToLowerInvariant() switch
        {
            "postgresql" => builder.UseNpgsql(connectionString, e => e.MigrationsAssembly("Boilerate.Migrators.PostgreSQL")),
            "mssql" => builder.UseSqlServer(connectionString,
                e => e.MigrationsAssembly("Boilerate.Migrators.MSSQL")),
            _ => throw new InvalidOperationException($"DB Provider '{provider}' not supported")
        };
    }
}
