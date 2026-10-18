using System.Text.Json;
using System.Text.Encodings.Web;
using Boilerate.Application.Common.Mailing;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Media;
using Boilerate.Domain.Messaging;
using Boilerate.Domain.Ordering;
using Boilerate.Infrastructure.FileStorage;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace Boilerate.Infrastructure.Commerce;

public sealed class OutboxProcessor(ApplicationDbContext db, CommerceUnitOfWork work, LocalFileStorageService storage,
    IOptions<CommerceSettings> settings, ILogger<OutboxProcessor> logger, IMailService mail)
{
    public async Task<bool> RunOneAsync(CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Run outbox consumers outside a database transaction.");
        var now = DateTime.UtcNow;
        var id = await db.OutboxMessages.AsNoTracking().Where(m => (m.State == OutboxState.Pending && m.NextAttemptAt <= now) ||
            (m.State == OutboxState.Processing && m.LeaseUntil <= now)).OrderBy(m => m.NextAttemptAt).ThenBy(m => m.MessageId)
            .Select(m => (Guid?)m.MessageId).FirstOrDefaultAsync(ct);
        if (!id.HasValue)
            return false;
        var token = Guid.NewGuid();
        var until = now.AddSeconds(settings.Value.LeaseSeconds);
        int claimed = await db.OutboxMessages.Where(m => m.MessageId == id && ((m.State == OutboxState.Pending && m.NextAttemptAt <= now) ||
            (m.State == OutboxState.Processing && m.LeaseUntil <= now))).ExecuteUpdateAsync(s => s.SetProperty(m => m.State, OutboxState.Processing)
                .SetProperty(m => m.LeaseToken, token).SetProperty(m => m.LeaseUntil, until).SetProperty(m => m.Attempts, m => m.Attempts + 1), ct);
        if (claimed != 1)
            return true;
        var message = await db.OutboxMessages.AsNoTracking().SingleAsync(m => m.MessageId == id, ct);
        try
        {
            if (message.SchemaVersion != 1)
                throw new InvalidOperationException("Unsupported message schema.");
            switch (message.Type)
            {
                case "MediaCleanupRequested":
                    var cleanup = JsonSerializer.Deserialize<MediaCleanupRequested>(message.Payload)
                        ?? throw new InvalidOperationException("Invalid media cleanup payload.");
                    await CleanupAsync(cleanup.MediaId, ct);
                    break;
                case "DigitalDeliveryReady":
                    var delivery = JsonSerializer.Deserialize<DigitalDeliveryReady>(message.Payload)
                        ?? throw new InvalidOperationException("Invalid digital delivery notification payload.");
                    await NotifyDigitalDeliveryAsync(delivery.OrderId, message.MessageId, ct);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported message type.");
            }
            await db.OutboxMessages.Where(m => m.MessageId == id && m.LeaseToken == token && m.State == OutboxState.Processing)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.State, OutboxState.Completed).SetProperty(m => m.LeaseToken, (Guid?)null)
                    .SetProperty(m => m.LeaseUntil, (DateTime?)null).SetProperty(m => m.LastError, (string?)null), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            db.ChangeTracker.Clear();
            var state = message.Attempts >= settings.Value.MaximumAttempts ? OutboxState.DeadLetter : OutboxState.Pending;
            var retry = DateTime.UtcNow.AddSeconds(Math.Min(3600, Math.Pow(2, message.Attempts) * 5));
            await db.OutboxMessages.Where(m => m.MessageId == id && m.LeaseToken == token && m.State == OutboxState.Processing)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.State, state).SetProperty(m => m.NextAttemptAt, retry)
                    .SetProperty(m => m.LastError, ex.GetType().Name).SetProperty(m => m.LeaseToken, (Guid?)null).SetProperty(m => m.LeaseUntil, (DateTime?)null), ct);
            logger.LogWarning("Outbox {MessageId} failed with {ErrorType}; state {State}", id, ex.GetType().Name, state);
        }
        return true;
    }

    private async Task NotifyDigitalDeliveryAsync(Guid orderId, Guid messageId, CancellationToken ct)
    {
        // Read only delivery existence and order contact metadata. Secrets never enter mail requests.
        var order = await db.Orders.AsNoTracking().Where(o => o.Id == orderId && o.PaymentStatus == OrderPaymentStatus.Paid)
            .Select(o => new { o.Number, o.Email }).SingleOrDefaultAsync(ct);
        if (order is null || !await db.DigitalDeliveries.AnyAsync(d => d.OrderId == orderId, ct))
            throw new InvalidOperationException("A paid order with delivered digital content is required for this notification.");
        var number = HtmlEncoder.Default.Encode(order.Number);
        var request = new MailRequest([order.Email], $"Đơn hàng {order.Number}: hàng số đã sẵn sàng",
            $"<p>Nội dung hàng số của đơn hàng <strong>{number}</strong> đã sẵn sàng.</p>" +
            "<p>Vui lòng đăng nhập và xem mục <strong>Đơn hàng của tôi</strong> để nhận nội dung.</p>",
            headers: new Dictionary<string, string>
            {
                // Stable across retries; SMTP delivery is still at-least-once, not deduplicated by this header.
                ["Message-Id"] = $"<outbox.{messageId:N}@boilerate.invalid>"
            });
        await mail.SendAsync(request, ct);
    }
    public async Task CleanupAsync(Guid mediaId, CancellationToken ct)
    {
        var key = await work.ExecuteAsync(async () =>
        {
            if (await db.MediaAssets.Where(m => m.Id == mediaId).ExecuteUpdateAsync(s => s.SetProperty(m => m.Version, m => m.Version), ct) == 0)
                return null;
            var asset = await db.MediaAssets.SingleAsync(m => m.Id == mediaId, ct);
            if (asset.State == MediaState.Deleted || await db.Set<ProductMedia>().AnyAsync(m => m.MediaAssetId == mediaId, ct))
                return null;
            if (asset.State == MediaState.Uploading && asset.ExpiresAt > DateTime.UtcNow)
                return null;
            asset.BeginDeletion();
            return asset.StorageKey;
        }, ct);
        if (key is null)
            return;
        storage.Remove(key); // Idempotent, outside the database transaction; keys are never reused.
        await work.ExecuteAsync(async () =>
        {
            await db.MediaAssets.Where(m => m.Id == mediaId).ExecuteUpdateAsync(s => s.SetProperty(m => m.Version, m => m.Version), ct);
            var asset = await db.MediaAssets.SingleAsync(m => m.Id == mediaId, ct);
            asset.CompleteDeletion();
            return true;
        }, ct);
    }
    public async Task SweepAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var requested = (await db.OutboxMessages.AsNoTracking()
            .Where(m => m.Type == "MediaCleanupRequested" && m.State != OutboxState.Completed)
            .Select(m => m.Payload).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        var candidates = db.MediaAssets.AsNoTracking().Where(m => m.State != MediaState.Deleted && m.ExpiresAt <= now &&
            !db.Set<ProductMedia>().Any(p => p.MediaAssetId == m.Id)).OrderBy(m => m.ExpiresAt).ThenBy(m => m.Id);
        int offset = 0, added = 0;
        while (added < 100)
        {
            var ids = await candidates.Skip(offset).Take(100).Select(m => m.Id).ToListAsync(ct);
            foreach (var id in ids)
            {
                var message = OutboxMessage.Cleanup(id,Guid.Empty);
                if (requested.Add(message.Payload)) { db.OutboxMessages.Add(message); added++; }
                if (added == 100) break;
            }
            if (ids.Count < 100) break;
            offset += ids.Count; // Pending/dead-letter assets must not starve later abandoned uploads.
        }
        await db.SaveChangesAsync(ct);
    }
}
