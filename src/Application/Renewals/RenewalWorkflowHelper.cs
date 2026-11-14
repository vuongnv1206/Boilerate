using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Ordering.Shared;
using Boilerate.Domain.Auditing;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Renewals;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Renewals;

internal static class RenewalWorkflowHelper
{
    internal static IQueryable<RenewalRequest> RenewalRequests(IApplicationDbContext db) =>
        db.RenewalRequests.Include(r => r.Attempts);

    internal static IDataProtector TargetProtector(IDataProtectionProvider protection, Guid id) =>
        protection.CreateProtector("Boilerate.Renewals.Target.v1", id.ToString("N"));

    internal static IDataProtector EvidenceProtector(IDataProtectionProvider protection, Guid id) =>
        protection.CreateProtector("Boilerate.Renewals.Evidence.v1", id.ToString("N"));

    internal static async Task<Order> LoadRenewalOrderAsync(
        IApplicationDbContext db,
        Guid id,
        Guid? ownerId,
        CancellationToken ct) =>
        await OrderStockCoordinator.Orders(db)
            .SingleOrDefaultAsync(o => o.Id == id && (!ownerId.HasValue || o.CustomerId == ownerId.Value), ct)
        ?? throw new NotFoundException("Order not found.");

    /// <summary>
    /// The order as its buyer may see it: a signed-in customer by id, a guest by the email they verified.
    /// Exactly one of <paramref name="customer"/> and <paramref name="guestEmail"/> is given.
    /// </summary>
    internal static async Task<Order> LoadBuyerOrderAsync(
        IApplicationDbContext db,
        Guid id,
        Guid? customer,
        string? guestEmail,
        CancellationToken ct)
    {
        if (customer.HasValue)
        {
            return await LoadRenewalOrderAsync(db, id, customer, ct);
        }

        string email = OrderingAuthHelper.NormalizeEmail(guestEmail!);
        return await OrderStockCoordinator.Orders(db)
            .SingleOrDefaultAsync(o => o.Id == id && o.IsGuestOrder && o.NormalizedEmail == email, ct)
            ?? throw new NotFoundException("Order not found.");
    }

    internal static async Task<List<RenewalDto>> ListForBuyerAsync(
        IApplicationDbContext db,
        Guid orderId,
        Guid? customer,
        string? guestEmail,
        CancellationToken ct)
    {
        var order = await LoadBuyerOrderAsync(db, orderId, customer, guestEmail, ct);
        var requests = await db.RenewalRequests.AsNoTracking()
            .Where(r => r.OrderId == orderId)
            .OrderBy(r => r.Id)
            .ToListAsync(ct);
        return requests.Select(r => MapRenewal(r, order.Items.Single(i => i.Id == r.OrderItemId))).ToList();
    }

    internal static Task<RenewalDto> SetTargetForBuyerAsync(
        IApplicationDbContext db,
        ICommerceUnitOfWork work,
        IDataProtectionProvider protection,
        DateTime nowUtc,
        Guid orderId,
        Guid id,
        SetRenewalTarget input,
        Guid? customer,
        string? guestEmail,
        CancellationToken ct)
    {
        string target = RenewalRequest.NormalizeTarget(input.AccountIdentifier);
        string hash = OrderFulfillmentCoordinator.RenewalHash(new { AccountIdentifier = target });
        return work.ExecuteAsync(async () =>
        {
            await OrderingAuthHelper.AcquireOrderingGateAsync(db, ct);
            var order = await LoadBuyerOrderAsync(db, orderId, customer, guestEmail, ct);
            // A guest has no user id; the id of their order's cart owner stands in as the actor.
            Guid actorId = customer ?? order.CustomerId;
            var renewal = await RenewalRequests(db)
                .SingleOrDefaultAsync(r => r.Id == id && r.OrderId == orderId, ct)
                ?? throw new NotFoundException("Renewal not found.");
            if (!await ReplayAsync(db, id, input.OperationId, actorId, "TargetSet", hash, ct))
            {
                CommerceConcurrency.CheckVersion(input.Version, renewal.Version);
                RequireRenewalOrder(order);
                string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(target.Normalize(NormalizationForm.FormKC).ToUpperInvariant())));
                string preview = target.Length > 4 ? $"***{target[^4..]}" : "***";
                renewal.SetTarget(TargetProtector(protection, id).Protect(target), key, preview);
                OrderFulfillmentCoordinator.RecordRenewalOperation(db, renewal, input.OperationId, actorId, "TargetSet", hash, nowUtc);
            }

            return MapRenewal(renewal, order.Items.Single(i => i.Id == renewal.OrderItemId));
        }, ct);
    }

    internal static Task<RenewalContentDto> RevealTargetForBuyerAsync(
        IApplicationDbContext db,
        ICommerceUnitOfWork work,
        IDataProtectionProvider protection,
        DateTime nowUtc,
        Guid orderId,
        Guid id,
        Guid? customer,
        string? guestEmail,
        CancellationToken ct) =>
        work.ExecuteAsync(async () =>
        {
            var order = await LoadBuyerOrderAsync(db, orderId, customer, guestEmail, ct);
            if (order.PaymentStatus != OrderPaymentStatus.Paid)
            {
                throw new ConflictException("Renewal history is available after payment.");
            }

            var renewal = await db.RenewalRequests
                .SingleOrDefaultAsync(r => r.Id == id && r.OrderId == orderId, ct)
                ?? throw new NotFoundException("Renewal not found.");
            return RevealTarget(db, protection, customer ?? order.CustomerId, nowUtc, renewal);
        }, ct);

    internal static void RequireRenewalOrder(Order order, bool processing = false)
    {
        if (order.PaymentStatus != OrderPaymentStatus.Paid ||
            order.Status is OrderStatus.Cancelled or OrderStatus.Expired or OrderStatus.Closed ||
            (processing && order.FulfillmentStatus == OrderFulfillmentStatus.NeedsReview))
        {
            throw new ConflictException("Renewals require a paid, non-cancelled order; resolve fulfillment review before processing.");
        }
    }

    internal static async Task<bool> ReplayAsync(
        IApplicationDbContext db,
        Guid id,
        Guid operationId,
        Guid actorId,
        string action,
        string hash,
        CancellationToken ct)
    {
        if (operationId == Guid.Empty)
        {
            throw new DomainException("OperationId is required.");
        }

        var prior = await db.RenewalOperations.SingleOrDefaultAsync(o => o.OperationId == operationId, ct);
        if (prior is null)
        {
            return false;
        }

        if (prior.RequestId != id || prior.ActorId != actorId || prior.Action != action || prior.RequestHash != hash)
        {
            throw new ConflictException("OperationId was already used by another actor, request or content.");
        }

        return true;
    }

    internal static RenewalDto MapRenewal(RenewalRequest r, OrderItem item) =>
        new(
            r.Id,
            r.OrderId,
            r.OrderItemId,
            r.Version,
            r.Status,
            item.Name,
            item.Sku,
            item.OptionDescription,
            item.Quantity,
            r.TargetPreview,
            CommerceConcurrency.UtcTimestamp(r.CompletedAtUtc),
            CommerceConcurrency.UtcTimestamp(r.VerifiedExpiresAtUtc));

    internal static RenewalAdminDto MapRenewalAdmin(
        RenewalRequest r,
        OrderItem item,
        Guid actorId,
        DateTime nowUtc,
        bool includeToken = false) =>
        new(
            MapRenewal(r, item),
            r.ClaimedBy,
            CommerceConcurrency.UtcTimestamp(r.ClaimUntilUtc),
            includeToken && r.Status == RenewalStatus.Claimed && r.ClaimedBy == actorId && r.ClaimUntilUtc > nowUtc
                ? r.ClaimToken
                : null,
            r.Attempts
                .OrderBy(a => a.StartedAtUtc)
                .ThenBy(a => a.Id)
                .Select(a => new RenewalAttemptDto(
                    a.Id,
                    a.OperationId,
                    a.StartedBy,
                    CommerceConcurrency.UtcTimestamp(a.StartedAtUtc),
                    a.Outcome,
                    a.VerifiedBy,
                    CommerceConcurrency.UtcTimestamp(a.VerifiedAtUtc),
                    CommerceConcurrency.UtcTimestamp(a.NewExpiresAtUtc)))
                .ToList());

    internal static Task<RenewalAdminDto> ChangeRenewalAsync(
        IApplicationDbContext db,
        ICommerceUnitOfWork work,
        Guid id,
        string version,
        Guid operationId,
        Guid actorId,
        DateTime nowUtc,
        string action,
        object payload,
        bool requireReadyOrder,
        Func<RenewalRequest, Order, Task> change,
        CancellationToken ct) =>
        work.ExecuteAsync(async () =>
        {
            await OrderingAuthHelper.AcquireOrderingGateAsync(db, ct);
            var request = await RenewalRequests(db).SingleOrDefaultAsync(r => r.Id == id, ct)
                ?? throw new NotFoundException("Renewal not found.");
            var order = await LoadRenewalOrderAsync(db, request.OrderId, null, ct);
            string hash = OrderFulfillmentCoordinator.RenewalHash(payload);
            if (!await ReplayAsync(db, id, operationId, actorId, action, hash, ct))
            {
                CommerceConcurrency.CheckVersion(version, request.Version);
                if (requireReadyOrder)
                {
                    RequireRenewalOrder(order, true);
                }

                await change(request, order);
                OrderFulfillmentCoordinator.RecordRenewalOperation(db, request, operationId, actorId, action, hash, nowUtc);
            }

            return MapRenewalAdmin(request, order.Items.Single(i => i.Id == request.OrderItemId), actorId, nowUtc, true);
        }, ct);

    internal static void ReadAudit(
        IApplicationDbContext db,
        Guid actorId,
        DateTime nowUtc,
        Guid id,
        string table,
        string action) =>
        db.Trails.Add(new Trail
        {
            UserId = actorId,
            TableName = table,
            PrimaryKey = id.ToString(),
            Type = TrailType.Read,
            DateTime = nowUtc,
            NewValues = JsonSerializer.Serialize(new { Action = action }),
        });

    internal static RenewalContentDto RevealTarget(
        IApplicationDbContext db,
        IDataProtectionProvider protection,
        Guid actorId,
        DateTime nowUtc,
        RenewalRequest request)
    {
        if (request.ProtectedTarget is null)
        {
            throw new ConflictException("The renewal has no account identifier.");
        }

        string content = TargetProtector(protection, request.Id).Unprotect(request.ProtectedTarget);
        ReadAudit(db, actorId, nowUtc, request.Id, "RenewalRequests", "RevealTarget");
        return new RenewalContentDto(request.Id, content);
    }
}

