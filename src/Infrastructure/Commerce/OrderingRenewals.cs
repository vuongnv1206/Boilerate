using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Boilerate.Application.Catalog;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Ordering;
using Boilerate.Domain.Auditing;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Renewals;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Infrastructure.Commerce;

public sealed partial class OrderingService
{
    private IQueryable<RenewalRequest> RenewalRequests() => db.RenewalRequests.Include(r => r.Attempts);

    private async Task RenewalPermission(string action, CancellationToken ct)
    {
        if (!await users.HasPermissionAsync(Customer().ToString(), $"Permissions.Renewal.{action}", ct))
            throw new ForbiddenException($"Renewal.{action} is required.");
    }

    private IDataProtector TargetProtector(Guid id) => protection.CreateProtector("Boilerate.Renewals.Target.v1", id.ToString("N"));
    private IDataProtector EvidenceProtector(Guid id) => protection.CreateProtector("Boilerate.Renewals.Evidence.v1", id.ToString("N"));
    private static string RenewalHash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));

    private async Task<bool> RenewalReplay(Guid id, Guid operationId, string action, string hash, CancellationToken ct)
    {
        if (operationId == Guid.Empty) throw new DomainException("OperationId is required.");
        var prior = await db.RenewalOperations.SingleOrDefaultAsync(o => o.OperationId == operationId, ct);
        if (prior is null) return false;
        if (prior.RequestId != id || prior.ActorId != Customer() || prior.Action != action || prior.RequestHash != hash)
            throw new ConflictException("OperationId was already used by another actor, request or content.");
        return true;
    }

    private void RecordRenewal(RenewalRequest request, Guid operationId, string action, string hash) =>
        db.RenewalOperations.Add(new(request.Id, operationId, Customer(), action, hash, request.Status, Now));

    // Runs in the receipt/provision transaction, never as a side effect of reading or replaying a receipt.
    private async Task EnsureRenewalRequests(Order order, CancellationToken ct)
    {
        if (order.PaymentStatus != OrderPaymentStatus.Paid || order.Status == OrderStatus.Cancelled) return;
        var existing = await db.RenewalRequests.Where(r => r.OrderId == order.Id).Select(r => r.OrderItemId).ToListAsync(ct);
        foreach (var item in order.Items.Where(i => i.FulfillmentKind == ProductFulfillmentKind.ManualExtension && !existing.Contains(i.Id)))
        {
            var request = new RenewalRequest(order.Id, item.Id, item.VariantId);
            db.RenewalRequests.Add(request);
            RecordRenewal(request, Guid.NewGuid(), "Created", RenewalHash(new { item.Id }));
        }
    }

    private async Task<Order> RenewalOrder(Guid id, bool owner, CancellationToken ct) =>
        await Orders().SingleOrDefaultAsync(o => o.Id == id && (!owner || o.CustomerId == actor.GetUserId()), ct)
            ?? throw new NotFoundException("Order not found.");

    private static void RequireRenewalOrder(Order order, bool processing = false)
    {
        if (order.PaymentStatus != OrderPaymentStatus.Paid || order.Status is OrderStatus.Cancelled or OrderStatus.Expired ||
            (processing && order.FulfillmentStatus == OrderFulfillmentStatus.NeedsReview))
            throw new ConflictException("Renewals require a paid, non-cancelled order; resolve fulfillment review before processing.");
    }

    private static RenewalDto MapRenewal(RenewalRequest r, OrderItem item) => new(r.Id, r.OrderId, r.OrderItemId, r.Version,
        r.Status, item.Name, item.Sku, item.OptionDescription, item.Quantity, r.TargetPreview,
        UtcTimestamp(r.CompletedAtUtc), UtcTimestamp(r.VerifiedExpiresAtUtc));

    private RenewalAdminDto MapRenewalAdmin(RenewalRequest r, OrderItem item, bool includeToken = false) => new(MapRenewal(r, item),
        r.ClaimedBy, UtcTimestamp(r.ClaimUntilUtc), includeToken && r.Status == RenewalStatus.Claimed && r.ClaimedBy == actor.GetUserId() &&
        r.ClaimUntilUtc > Now ? r.ClaimToken : null,
        r.Attempts.OrderBy(a => a.StartedAtUtc).ThenBy(a => a.Id).Select(a => new RenewalAttemptDto(a.Id, a.OperationId,
            a.StartedBy, UtcTimestamp(a.StartedAtUtc), a.Outcome, a.VerifiedBy, UtcTimestamp(a.VerifiedAtUtc), UtcTimestamp(a.NewExpiresAtUtc))).ToList());

    public async Task<List<RenewalDto>> RenewalsAsync(Guid orderId, CancellationToken ct)
    {
        Customer();
        var order = await RenewalOrder(orderId, true, ct);
        var requests = await db.RenewalRequests.AsNoTracking().Where(r => r.OrderId == orderId).OrderBy(r => r.Id).ToListAsync(ct);
        return requests.Select(r => MapRenewal(r, order.Items.Single(i => i.Id == r.OrderItemId))).ToList();
    }

    public async Task<RenewalDto> SetRenewalTargetAsync(Guid orderId, Guid id, SetRenewalTarget input, CancellationToken ct)
    {
        Customer();
        var target = RenewalRequest.NormalizeTarget(input.AccountIdentifier);
        var hash = RenewalHash(new { AccountIdentifier = target });
        return await work.ExecuteAsync(async () =>
        {
            await Gate(ct);
            var order = await RenewalOrder(orderId, true, ct);
            var request = await RenewalRequests().SingleOrDefaultAsync(r => r.Id == id && r.OrderId == orderId, ct)
                ?? throw new NotFoundException("Renewal not found.");
            if (!await RenewalReplay(id, input.OperationId, "TargetSet", hash, ct))
            {
                CheckVersion(input.Version, request.Version);
                RequireRenewalOrder(order);
                var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(target.Normalize(NormalizationForm.FormKC).ToUpperInvariant())));
                var preview = target.Length > 4 ? $"***{target[^4..]}" : "***";
                request.SetTarget(TargetProtector(id).Protect(target), key, preview);
                RecordRenewal(request, input.OperationId, "TargetSet", hash);
            }
            return MapRenewal(request, order.Items.Single(i => i.Id == request.OrderItemId));
        }, ct);
    }

    public async Task<Page<RenewalAdminDto>> RenewalsAdminAsync(int page, int size, RenewalStatus? status, CancellationToken ct)
    {
        await RenewalPermission("View", ct);
        CommerceService.ValidatePage(page, size);
        if (status.HasValue && !Enum.IsDefined(status.Value)) throw new DomainException("Invalid renewal status.");
        var query = RenewalRequests().AsNoTracking().Where(r => !status.HasValue || r.Status == status);
        var total = await query.CountAsync(ct);
        var requests = await query.OrderBy(r => r.CreatedOn).ThenBy(r => r.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        var ids = requests.Select(r => r.OrderItemId).ToArray();
        var items = await db.Set<OrderItem>().AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        return new(requests.Select(r => MapRenewalAdmin(r, items[r.OrderItemId])).ToList(), total, page, size);
    }

    public async Task<RenewalAdminDto> GetRenewalAdminAsync(Guid id, CancellationToken ct)
    {
        await RenewalPermission("View", ct);
        var request = await RenewalRequests().AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Renewal not found.");
        return MapRenewalAdmin(request, await db.Set<OrderItem>().AsNoTracking().SingleAsync(i => i.Id == request.OrderItemId, ct), true);
    }

    public async Task<List<RenewalDto>> ProvisionRenewalsAsync(Guid orderId, ProvisionRenewals input, CancellationToken ct)
    {
        await RenewalPermission("Process", ct);
        return await work.ExecuteAsync(async () =>
        {
            await Gate(ct);
            var order = await RenewalOrder(orderId, false, ct);
            CheckVersion(input.OrderVersion, order.Version);
            RequireRenewalOrder(order);
            await EnsureRenewalRequests(order, ct);
            // Include newly provisioned rows without forcing a separate transaction.
            await db.SaveChangesAsync(ct);
            var requests = await db.RenewalRequests.Where(r => r.OrderId == orderId).OrderBy(r => r.Id).ToListAsync(ct);
            return requests.Select(r => MapRenewal(r, order.Items.Single(i => i.Id == r.OrderItemId))).ToList();
        }, ct);
    }

    private async Task<RenewalAdminDto> ChangeRenewal(Guid id, string version, Guid operationId, string action, object payload,
        bool requireReadyOrder, Func<RenewalRequest, Task> change, CancellationToken ct) => await work.ExecuteAsync(async () =>
    {
        await Gate(ct);
        var request = await RenewalRequests().SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Renewal not found.");
        var order = await RenewalOrder(request.OrderId, false, ct);
        var hash = RenewalHash(payload);
        if (!await RenewalReplay(id, operationId, action, hash, ct))
        {
            CheckVersion(version, request.Version);
            if (requireReadyOrder) RequireRenewalOrder(order, true);
            await change(request);
            RecordRenewal(request, operationId, action, hash);
        }
        return MapRenewalAdmin(request, order.Items.Single(i => i.Id == request.OrderItemId), true);
    }, ct);

    public async Task<RenewalAdminDto> ClaimRenewalAsync(Guid id, ClaimRenewal input, CancellationToken ct)
    {
        await RenewalPermission("Process", ct);
        return await ChangeRenewal(id, input.Version, input.OperationId, "Claim", new { }, true, async request =>
        {
            var competing = await db.RenewalRequests.Where(r => r.Id != id && r.TargetKey == request.TargetKey &&
                (r.Status == RenewalStatus.Claimed || r.Status == RenewalStatus.PendingVerification)).ToListAsync(ct);
            foreach (var other in competing)
            {
                if (!other.ReleaseExpired(Now)) throw new ConflictException("Another renewal for this account is claimed or awaiting verification.");
                RecordRenewal(other, Guid.NewGuid(), "ClaimExpired", RenewalHash(new { OtherRequestId = other.Id }));
            }
            if (request.ReleaseExpired(Now))
                RecordRenewal(request, Guid.NewGuid(), "ClaimExpired", RenewalHash(new { RequestId = id }));
            if (competing.Count > 0) await db.SaveChangesAsync(ct);
            request.Claim(Customer(), Now, settings.Value.RenewalClaimMinutes);
        }, ct);
    }

    public async Task<RenewalAdminDto> ReleaseRenewalAsync(Guid id, ReleaseRenewal input, CancellationToken ct)
    {
        await RenewalPermission("Process", ct);
        return await ChangeRenewal(id, input.Version, input.OperationId, "Release", new { }, false, request =>
        {
            request.Release(Customer(), input.ClaimToken, Now);
            return Task.CompletedTask;
        }, ct);
    }

    public async Task<RenewalAdminDto> StartRenewalAsync(Guid id, StartRenewal input, CancellationToken ct)
    {
        await RenewalPermission("Process", ct);
        return await ChangeRenewal(id, input.Version, input.OperationId, "Start", new { }, true, request =>
        {
            request.Start(Customer(), input.ClaimToken, input.OperationId, Now);
            return Task.CompletedTask;
        }, ct);
    }

    public async Task<RenewalAdminDto> VerifyRenewalAsync(Guid id, VerifyRenewal input, CancellationToken ct)
    {
        await RenewalPermission("Verify", ct);
        var note = CatalogRules.Required(input.Note, 1200, "Verification note");
        var evidence = string.IsNullOrWhiteSpace(input.Evidence) ? null : CatalogRules.Required(input.Evidence, 1800, "Evidence");
        if (input.NewExpiresAtUtc.HasValue && input.NewExpiresAtUtc.Value.Kind != DateTimeKind.Utc)
            throw new DomainException("Verified expiry must be UTC.");
        var expiry = input.NewExpiresAtUtc.HasValue ? new DateTime(input.NewExpiresAtUtc.Value.Ticks - input.NewExpiresAtUtc.Value.Ticks % 10, DateTimeKind.Utc) : (DateTime?)null;
        var payload = new { input.AttemptId, input.Applied, NewExpiresAtUtc = expiry, Note = note, Evidence = evidence };
        return await ChangeRenewal(id, input.Version, input.OperationId, input.Applied ? "VerifyApplied" : "VerifyNotApplied", payload, false, async request =>
        {
            request.Verify(input.AttemptId, input.Applied, expiry, Customer(), Now,
                EvidenceProtector(input.AttemptId).Protect(JsonSerializer.Serialize(new { Note = note, Evidence = evidence })));
            var order = await RenewalOrder(request.OrderId, false, ct);
            if (order.PaymentStatus == OrderPaymentStatus.Paid && order.Status is not (OrderStatus.Cancelled or OrderStatus.Expired) &&
                order.FulfillmentStatus != OrderFulfillmentStatus.NeedsReview)
                await ReconcileOrderFulfillment(order, ct);
        }, ct);
    }

    private void RenewalReadAudit(Guid id, string table, string action) => db.Trails.Add(new Trail
    {
        UserId = Customer(), TableName = table, PrimaryKey = id.ToString(), Type = TrailType.Read, DateTime = Now,
        NewValues = JsonSerializer.Serialize(new { Action = action })
    });

    public async Task<RenewalContentDto> RevealRenewalTargetAsync(Guid orderId, Guid id, CancellationToken ct)
    {
        Customer();
        return await work.ExecuteAsync(async () =>
        {
            RequireRenewalOrder(await RenewalOrder(orderId, true, ct));
            var request = await db.RenewalRequests.SingleOrDefaultAsync(r => r.Id == id && r.OrderId == orderId, ct)
                ?? throw new NotFoundException("Renewal not found.");
            return RevealTarget(request);
        }, ct);
    }

    private RenewalContentDto RevealTarget(RenewalRequest request)
    {
        if (request.ProtectedTarget is null) throw new ConflictException("The renewal has no account identifier.");
        var content = TargetProtector(request.Id).Unprotect(request.ProtectedTarget);
        RenewalReadAudit(request.Id, "RenewalRequests", "RevealTarget");
        return new(request.Id, content);
    }

    public async Task<RenewalContentDto> RevealRenewalTargetAdminAsync(Guid id, CancellationToken ct)
    {
        await RenewalPermission("Reveal", ct);
        return await work.ExecuteAsync(async () => RevealTarget(await db.RenewalRequests.SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Renewal not found.")), ct);
    }

    public async Task<RenewalContentDto> RevealRenewalEvidenceAsync(Guid id, Guid attemptId, CancellationToken ct)
    {
        await RenewalPermission("Verify", ct);
        return await work.ExecuteAsync(async () =>
        {
            var attempt = await db.RenewalAttempts.SingleOrDefaultAsync(a => a.RequestId == id && a.Id == attemptId, ct)
                ?? throw new NotFoundException("Attempt not found.");
            if (attempt.ProtectedEvidence is null) throw new ConflictException("This attempt has not been verified.");
            var content = EvidenceProtector(attemptId).Unprotect(attempt.ProtectedEvidence);
            RenewalReadAudit(attemptId, "RenewalAttempts", "RevealEvidence");
            return new RenewalContentDto(attemptId, content);
        }, ct);
    }

    public async Task<List<RenewalHistoryDto>> RenewalHistoryAsync(Guid id, CancellationToken ct)
    {
        await RenewalPermission("View", ct);
        if (!await db.RenewalRequests.AnyAsync(r => r.Id == id, ct)) throw new NotFoundException("Renewal not found.");
        var operations = await db.RenewalOperations.AsNoTracking().Where(o => o.RequestId == id)
            .OrderBy(o => o.OccurredAtUtc).ThenBy(o => o.Id).ToListAsync(ct);
        return operations.Select(o => new RenewalHistoryDto(o.OperationId, o.Action, o.ActorId, o.StatusAfter, UtcTimestamp(o.OccurredAtUtc))).ToList();
    }
}
