using Boilerate.Application.Notifications;
using Boilerate.Infrastructure.Auth;
using Boilerate.Infrastructure.Notifications.Hubs;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Boilerate.Infrastructure.Notifications;

/// <summary>Retries persisted deliveries after commit. Clients deduplicate by notification ID.</summary>
internal sealed class NotificationDispatcher(IServiceScopeFactory scopes, ILogger<NotificationDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await DispatchPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogError(ex, "Notification delivery failed; pending records will be retried."); }
        }
    }

    internal async Task DispatchPendingAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hub = scope.ServiceProvider.GetRequiredService<IHubContext<NotificationHub>>();
        var security = scope.ServiceProvider.GetRequiredService<IOptions<SecuritySettings>>().Value;
        var pending = await db.Notifications.Where(n => !n.IsSent &&
            db.NotificationReceipts.Any(r => r.NotificationId == n.Id))
            .OrderBy(n => n.CreatedOn).Take(100).ToListAsync(ct);
        foreach (var notification in pending)
        {
            var receipts = await db.NotificationReceipts.Where(r => r.NotificationId == notification.Id && r.HiddenOn == null)
                .Select(r => r.UserId).ToListAsync(ct);
            var candidates = receipts.Select(x => x.ToString()).ToArray();
            var now = DateTime.UtcNow;
            // A connection can outlive logout. Never address groups for revoked/expired sessions.
            var sessions = await db.UserSessions.Where(s => candidates.Contains(s.UserId) && s.RevokedOn == null &&
                    s.ExpiresOn > now && s.User.IsActive && (!security.RequireConfirmedAccount || s.User.EmailConfirmed) &&
                    (!s.User.LockoutEnabled || s.User.LockoutEnd == null || s.User.LockoutEnd <= DateTimeOffset.UtcNow))
                .Select(s => s.Id).ToListAsync(ct);
            if (sessions.Count != 0)
                await hub.Clients.Groups(sessions.Select(NotificationHub.SessionGroup).ToList()).SendAsync("ReceiveNotification", new NotificationPushDto
                {
                    Id = notification.Id, Title = notification.Title, Message = notification.Message,
                    Type = notification.Type, ActionUrl = notification.ActionUrl, CreatedOn = notification.CreatedOn
                }, ct);
            notification.MarkAsSent();
            await db.SaveChangesAsync(ct);
        }
    }
}
