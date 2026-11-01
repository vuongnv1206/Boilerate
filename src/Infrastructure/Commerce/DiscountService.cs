using Boilerate.Application.Catalog;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Ordering;
using Boilerate.Domain.Messaging;
using Boilerate.Domain.Ordering;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Infrastructure.Commerce;

public sealed class DiscountService(ApplicationDbContext db, CommerceUnitOfWork work) : IDiscountService
{
    private async Task Gate(CancellationToken ct)
    {
        if (await db.Set<CommerceLock>().Where(g => g.Name == "ordering")
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.Version, g => g.Version), ct) != 1)
            throw new ConflictException("Apply ordering migrations before changing discounts.");
    }

    private async Task<int> Count(Guid id, CancellationToken ct) =>
        await db.Orders.CountAsync(o => o.DiscountId == id && o.DiscountUsageHeld, ct);

    private static DiscountDto Map(Discount d, int count) => new(d.Id, d.Code, d.Name, d.Kind, d.Value,
        d.MinimumSubtotal, Utc(d.StartsAtUtc), Utc(d.EndsAtUtc), d.UsageLimit, count, d.IsActive, d.Version);
    private static DateTime? Utc(DateTime? value) => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;

    public async Task<Page<DiscountDto>> ListAsync(int page, int size, string? search, bool? isActive, CancellationToken ct)
    {
        CommerceService.ValidatePage(page, size);
        var query = db.Set<Discount>().AsNoTracking();
        if (isActive.HasValue) query = query.Where(d => d.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim().ToUpperInvariant();
            query = query.Where(d => d.Code.Contains(text) || d.Name.ToUpper().Contains(text));
        }
        var total = await query.CountAsync(ct);
        var discounts = await query.OrderByDescending(d => d.CreatedOn).ThenBy(d => d.Id)
            .Skip((page - 1) * size).Take(size).ToListAsync(ct);
        var ids = discounts.Select(d => d.Id).ToArray();
        var counts = await db.Orders.AsNoTracking().Where(o => o.DiscountUsageHeld && o.DiscountId.HasValue && ids.Contains(o.DiscountId.Value))
            .GroupBy(o => o.DiscountId!.Value).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        return new(discounts.Select(d => Map(d, counts.GetValueOrDefault(d.Id))).ToList(), total, page, size);
    }

    public async Task<DiscountDto> GetAsync(Guid id, CancellationToken ct)
    {
        var discount = await db.Set<Discount>().AsNoTracking().SingleOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new NotFoundException("Discount not found.");
        return Map(discount, await Count(id, ct));
    }

    public Task<DiscountDto> CreateAsync(SaveDiscount r, CancellationToken ct) => work.ExecuteAsync(async () =>
    {
        await Gate(ct);
        var discount = new Discount(r.Code, r.Name, r.Kind, r.Value, r.MinimumSubtotal, r.StartsAtUtc, r.EndsAtUtc, r.UsageLimit, r.IsActive);
        if (await db.Set<Discount>().AnyAsync(d => d.Code == discount.Code, ct)) throw new ConflictException("Discount code already exists.");
        db.Set<Discount>().Add(discount);
        return Map(discount, 0);
    }, ct);

    public Task<DiscountDto> UpdateAsync(Guid id, SaveDiscount r, CancellationToken ct) => work.ExecuteAsync(async () =>
    {
        await Gate(ct);
        var discount = await db.Set<Discount>().SingleOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new NotFoundException("Discount not found.");
        if (string.IsNullOrWhiteSpace(r.Version) || r.Version != discount.Version) throw new ConflictException("Version changed; reload before editing.");
        var count = await Count(id, ct);
        if (r.UsageLimit < count) throw new ConflictException("Usage limit cannot be below the current usage count.");
        discount.Update(r.Code, r.Name, r.Kind, r.Value, r.MinimumSubtotal, r.StartsAtUtc, r.EndsAtUtc, r.UsageLimit, r.IsActive);
        if (await db.Set<Discount>().AnyAsync(d => d.Id != id && d.Code == discount.Code, ct)) throw new ConflictException("Discount code already exists.");
        return Map(discount, count);
    }, ct);
}
