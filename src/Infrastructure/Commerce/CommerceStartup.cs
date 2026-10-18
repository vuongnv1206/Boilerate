using Boilerate.Application.Catalog;
using Boilerate.Application.Common.FileStorage;
using Boilerate.Infrastructure.FileStorage;
using Boilerate.Infrastructure.Persistence.Context;
using Boilerate.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Boilerate.Application.Ordering;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace Boilerate.Infrastructure.Commerce;

public static class CommerceStartup
{
    public static IServiceCollection AddCommerce(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<CommerceSettings>().Bind(config.GetSection("Commerce")).ValidateDataAnnotations().ValidateOnStart();
        services.AddScoped<CommerceUnitOfWork>();
        services.AddScoped<ICommerceService, CommerceService>();
        services.AddScoped<ICommerceMediaService, CommerceMediaService>();
        services.AddOptions<OrderingSettings>().Bind(config.GetSection("Ordering")).ValidateDataAnnotations()
            .Validate(s => decimal.Truncate(s.FlatShippingAmount) == s.FlatShippingAmount, "Shipping must be whole VND.").ValidateOnStart();
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<OrderingService>();
        services.AddScoped<IOrderingService>(sp => sp.GetRequiredService<OrderingService>());
        services.AddScoped<IPaymentService>(sp => sp.GetRequiredService<OrderingService>());
        services.AddScoped<OutboxProcessor>();
        services.AddSingleton(new LocalFileStorageService(Path.GetFullPath(config["Storage:Root"] ?? "Files")));
        services.AddSingleton<IFileStorageService>(sp => sp.GetRequiredService<LocalFileStorageService>());
        if (config.GetValue<bool>("Modules:Catalog:Enabled") && config.GetValue("Commerce:WorkerEnabled", true))
        {
            services.AddHostedService<CommerceWorker>();
            services.AddHostedService<OrderExpirationWorker>();
        }
        return services;
    }
}
internal sealed class OrderExpirationWorker(IServiceScopeFactory scopes, IOptions<OrderingSettings> settings,
    ILogger<OrderExpirationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.Value.WorkerIntervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IOrderingService>().ExpireAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Order expiration failed: {ErrorType}", ex.GetType().Name); }
        }
    }
}
internal sealed class CommerceWorker(IServiceScopeFactory scopes, IOptions<CommerceSettings> settings, ILogger<CommerceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.Value.WorkerIntervalSeconds));
        int tick = 0;
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using (var scope = scopes.CreateScope())
                {
                    if (tick % 12 == 0)
                        await scope.ServiceProvider.GetRequiredService<OutboxProcessor>().SweepAsync(stoppingToken);
                }
                for (int i = 0; i < 20; i++)
                {
                    using var scope = scopes.CreateScope();
                    if (!await scope.ServiceProvider.GetRequiredService<OutboxProcessor>().RunOneAsync(stoppingToken))
                        break;
                }
                if (tick++ % 12 == 0)
                {
                    using var scope = scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var pending = db.OutboxMessages.Where(m => m.State == OutboxState.Pending || m.State == OutboxState.Processing);
                    var oldest = await pending.MinAsync(m => (DateTime?)m.OccurredAt, stoppingToken);
                    logger.LogInformation("Outbox backlog={Backlog}, oldestAgeSeconds={Age}, deadLetters={DeadLetters}", await pending.CountAsync(stoppingToken),
                        oldest.HasValue ? (DateTime.UtcNow - oldest.Value).TotalSeconds : 0, await db.OutboxMessages.CountAsync(m => m.State == OutboxState.DeadLetter, stoppingToken));
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Commerce worker iteration failed: {ErrorType}", ex.GetType().Name); }
        }
    }
}
