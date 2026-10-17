using Boilerate.Application.Common.BackgroundJobs;
using Hangfire;
using Hangfire.PostgreSql;
using Hangfire.MemoryStorage;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace Boilerate.Infrastructure.BackgroundJobs;

internal static class Startup
{
    internal static IServiceCollection AddBackgroundJobs(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<HangfireStorageSettings>(config.GetSection(nameof(HangfireStorageSettings)));
        var settings = config.GetSection(nameof(HangfireStorageSettings)).Get<HangfireStorageSettings>();

        if (settings == null) throw new InvalidOperationException("HangfireStorageSettings is not configured.");

        if (!settings.Enabled) return services.AddTransient<IJobService, DisabledJobService>();

        services.AddHangfire((sp, configuration) =>
        {
            configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings();

            switch (settings.StorageProvider.ToLowerInvariant())
            {
                case "sqlserver":
                    if (string.IsNullOrEmpty(settings.ConnectionString))
                        throw new InvalidOperationException("SQL Server connection string is required for Hangfire storage.");

                    configuration.UseSqlServerStorage(settings.ConnectionString, new SqlServerStorageOptions
                    {
                        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                        QueuePollInterval = TimeSpan.Zero,
                        UseRecommendedIsolationLevel = true,
                        DisableGlobalLocks = true,
                        SchemaName = "hangfire"
                    });
                    break;

                case "postgresql":
                    if (string.IsNullOrWhiteSpace(settings.ConnectionString)) throw new InvalidOperationException("PostgreSQL job connection is required.");
                    configuration.UsePostgreSqlStorage(options => options.UseNpgsqlConnection(settings.ConnectionString));
                    break;
                case "memory":
                    if (!sp.GetRequiredService<IHostEnvironment>().IsDevelopment()) throw new InvalidOperationException("In-memory jobs are only supported in Development.");
                    configuration.UseMemoryStorage();
                    break;

                default:
                    throw new InvalidOperationException($"Unknown Hangfire storage provider: {settings.StorageProvider}");
            }

            configuration.UseFilter(new HangfireJobFilter(sp.GetRequiredService<ILogger<HangfireJobFilter>>()));

            if (settings.EnableAutomaticRetry)
            {
                configuration.UseFilter(new AutomaticRetryAttribute
                {
                    Attempts = settings.MaxRetryAttempts,
                    OnAttemptsExceeded = AttemptsExceededAction.Delete
                });
            }
        });

        services.AddHangfireServer(options =>
        {
            options.WorkerCount = settings.WorkerCount;
            options.ServerName = $"{Environment.MachineName}:{Guid.NewGuid()}";
        });

        return services.AddTransient<IJobService, HangfireService>();
    }

    internal static IApplicationBuilder UseHangfireDashboard(this IApplicationBuilder app, IConfiguration config)
    {
        var settings = config.GetSection(nameof(HangfireStorageSettings)).Get<HangfireStorageSettings>();

        if (settings?.Enabled == true && settings.EnableDashboard)
        {
            app.UseHangfireDashboard(settings.DashboardPath, new DashboardOptions
            {
                Authorization = new[] { new HangfireDashboardAuthorizationFilter() },
                DashboardTitle = settings.DashboardTitle,
                StatsPollingInterval = 5000,
                DisplayStorageConnectionString = false
            });
        }

        return app;
    }
}
