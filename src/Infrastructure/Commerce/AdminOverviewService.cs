using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Ordering;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.DigitalInventory;
using Boilerate.Domain.Inventory;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Boilerate.Infrastructure.Commerce;

public sealed class AdminOverviewService(ApplicationDbContext db, ICurrentUser actor, IUserService users,
    TimeProvider clock) : IAdminOverviewService
{
    private async Task<bool> CanAsync(string function, CancellationToken ct)
    {
        if (!actor.IsAuthenticated() || actor.GetUserId() == Guid.Empty)
            throw new UnauthorizedException("Sign in to view the overview.");
        return await users.HasPermissionAsync(actor.GetUserId().ToString(), $"Permissions.{function}.View", ct);
    }

    public async Task<OverviewWorkDto> WorkAsync(CancellationToken ct)
    {
        var orders = await CanAsync("Order", ct);
        var renewals = await CanAsync("Renewal", ct);
        if (!orders && !renewals) throw new ForbiddenException("Order.View or Renewal.View is required.");
        var now = clock.GetUtcNow().UtcDateTime;
        var groups = new List<OverviewWorkGroup>();
        if (orders)
        {
            foreach (var key in new[] { "awaiting-payment", "awaiting-processing", "needs-review" })
            {
                var query = OverviewQueues.Orders(db.Orders.AsNoTracking(), key, now);
                var total = await query.CountAsync(ct);
                var items = await query.OrderBy(o => o.CreatedOn).ThenBy(o => o.Id).Take(5)
                    .Select(o => new OverviewWorkItem(o.Id, o.Id, o.Number, null, null, (int)o.Status, o.CreatedOn)).ToListAsync(ct);
                groups.Add(new(key, "order", total, Normalize(items)));
            }
        }
        if (renewals)
        {
            foreach (var key in new[] { "awaiting-work", "pending-verification" })
            {
                var query = OverviewQueues.Renewals(db.RenewalRequests.AsNoTracking(), db.Orders, key);
                var total = await query.CountAsync(ct);
                var items = await (from r in query
                    join o in db.Orders on r.OrderId equals o.Id
                    join item in db.OrderItems on r.OrderItemId equals item.Id
                    orderby r.CreatedOn, r.Id
                    select new OverviewWorkItem(r.Id, o.Id, o.Number, item.Name, item.Sku, (int)r.Status, r.CreatedOn))
                    .Take(5).ToListAsync(ct);
                groups.Add(new("renewal-" + key, "renewal", total, Normalize(items)));
            }
        }
        return new(now, groups);
    }

    public async Task<OverviewFinanceDto> FinanceAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        if (!await CanAsync("FinancialSummary", ct)) throw new ForbiddenException("FinancialSummary.View is required.");
        var now = clock.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now.AddHours(7));
        if (from.HasValue != to.HasValue) throw new DomainException("Supply both from and to dates.");
        var start = from ?? today.AddDays(-6);
        var end = to ?? today;
        if (start == DateOnly.MinValue || start > end || end > today || end.DayNumber - start.DayNumber >= 90)
            throw new DomainException("Choose an ordered date range of at most 90 days ending no later than today.");
        // Vietnam has a stable UTC+07 offset. Convert the inclusive business dates to a half-open UTC range.
        var fromUtc = DateTime.SpecifyKind(start.ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(end.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);
        var receipts = await db.PaymentReceipts.AsNoTracking().Where(r => r.ReceivedAtUtc >= fromUtc && r.ReceivedAtUtc < toUtc)
            .GroupBy(r => r.ReceivedAtUtc.AddHours(7).Date)
            .Select(g => new { Date = g.Key, Amount = g.Sum(r => r.Amount) }).ToListAsync(ct);
        var refunds = await db.Refunds.AsNoTracking().Where(r => r.RefundedAtUtc >= fromUtc && r.RefundedAtUtc < toUtc)
            .GroupBy(r => r.RefundedAtUtc.AddHours(7).Date)
            .Select(g => new { Date = g.Key, Amount = g.Sum(r => r.Amount) }).ToListAsync(ct);
        var receivedByDate = receipts.ToDictionary(r => DateOnly.FromDateTime(r.Date), r => r.Amount);
        var refundedByDate = refunds.ToDictionary(r => DateOnly.FromDateTime(r.Date), r => r.Amount);
        var days = Enumerable.Range(0, end.DayNumber - start.DayNumber + 1).Select(offset =>
        {
            var date = start.AddDays(offset);
            var received = receivedByDate.GetValueOrDefault(date);
            var refunded = refundedByDate.GetValueOrDefault(date);
            return new OverviewFinanceDay(date, Money(received), Money(refunded), Money(received - refunded));
        }).ToList();
        var receivedTotal = receipts.Sum(d => d.Amount);
        var refundedTotal = refunds.Sum(d => d.Amount);
        return new(now, start, end, "Asia/Ho_Chi_Minh", "VND", Money(receivedTotal), Money(refundedTotal),
            Money(receivedTotal - refundedTotal), days);
    }

    public async Task<OverviewStockDto> StockAsync(int threshold, int page, int size, CancellationToken ct)
    {
        var physical = await CanAsync("Inventory", ct);
        var digital = await CanAsync("DigitalInventory", ct);
        if (!physical && !digital) throw new ForbiddenException("Inventory.View or DigitalInventory.View is required.");
        if (threshold < 0 || threshold > 999 || page < 1 || size is not (10 or 20 or 50) || page > int.MaxValue / size)
            throw new DomainException("Invalid stock threshold or page.");
        var now = clock.GetUtcNow().UtcDateTime;
        var query = from variant in db.ProductVariants.AsNoTracking()
            join product in db.Products.AsNoTracking() on variant.ProductId equals product.Id
            where variant.IsActive && !variant.IsRetired && product.Status == ProductStatus.Published &&
                ((physical && product.FulfillmentKind == ProductFulfillmentKind.Physical && variant.TrackInventory) ||
                 (digital && (product.FulfillmentKind == ProductFulfillmentKind.DigitalUnique ||
                              product.FulfillmentKind == ProductFulfillmentKind.DigitalShared)))
            select new StockProjection
            {
                ProductId = product.Id, VariantId = variant.Id, ProductName = product.Name, Sku = variant.Sku,
                FulfillmentKind = product.FulfillmentKind,
                Available = product.FulfillmentKind == ProductFulfillmentKind.Physical
                    ? db.InventoryItems.Where(i => i.VariantId == variant.Id &&
                        db.Set<InventoryLocation>().Any(l => l.Id == i.LocationId && l.Code == "MAIN"))
                        .Select(i => (int?)(i.OnHand - i.Reserved)).FirstOrDefault() ?? 0
                    : db.DigitalStockItems.Count(i => i.VariantId == variant.Id && i.State == DigitalStockState.Available)
            };
        query = query.Where(i => i.FulfillmentKind == ProductFulfillmentKind.DigitalShared ? i.Available == 0 : i.Available <= threshold);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(i => i.Available).ThenBy(i => i.ProductName).ThenBy(i => i.VariantId)
            .Skip((page - 1) * size).Take(size)
            .Select(i => new OverviewStockItem(i.ProductId, i.VariantId, i.ProductName, i.Sku, i.FulfillmentKind, i.Available)).ToListAsync(ct);
        return new(now, threshold, page, size, total, items);
    }

    private static List<OverviewWorkItem> Normalize(List<OverviewWorkItem> items) => items.Select(i => i with
    {
        CreatedAtUtc = DateTime.SpecifyKind(i.CreatedAtUtc, DateTimeKind.Utc)
    }).ToList();

    private static string Money(decimal value) => value.ToString("0", CultureInfo.InvariantCulture);

    private sealed class StockProjection
    {
        public Guid ProductId { get; init; }
        public Guid VariantId { get; init; }
        public string ProductName { get; init; } = string.Empty;
        public string Sku { get; init; } = string.Empty;
        public ProductFulfillmentKind FulfillmentKind { get; init; }
        public int Available { get; init; }
    }
}
