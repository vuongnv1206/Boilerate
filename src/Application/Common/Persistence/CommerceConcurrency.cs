using Boilerate.Application.Common.Exceptions;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;
using Boilerate.Domain.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Common.Persistence;

public static class CommerceConcurrency
{
    public static void CheckVersion(string? expected, string current, string message = "Version changed; reload before editing.")
    {
        if (string.IsNullOrWhiteSpace(expected) || expected != current)
        {
            throw new ConflictException(message);
        }
    }

    public static void ValidatePage(int page, int size)
    {
        if (page < 1 || size is < 1 or > 100 || (long)(page - 1) * size > int.MaxValue)
        {
            throw new DomainException("Invalid pagination; maximum page size is 100.");
        }
    }

    public static DateTime UtcTimestamp(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public static DateTime? UtcTimestamp(DateTime? value) => value.HasValue ? UtcTimestamp(value.Value) : null;

    public static async Task AcquireGateAsync(
        IApplicationDbContext db,
        string name,
        CancellationToken ct,
        string missingMessage = "Apply commerce migrations first.")
    {
        if (await db.Set<CommerceLock>()
                .Where(g => g.Name == name)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.Version, g => g.Version), ct) != 1)
        {
            throw new ConflictException(missingMessage);
        }
    }

    public static async Task<ProductType> LockProductTypeAsync(IApplicationDbContext db, Guid id, CancellationToken ct)
    {
        if (await db.ProductTypes
                .Where(x => x.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Version, x => x.Version), ct) != 1)
        {
            throw new NotFoundException("Product type not found.");
        }

        // Serializes product writes with schema edits without changing schema version.
        return await db.ProductTypes
            .Include(x => x.Fields)
            .ThenInclude(x => x.Choices)
            .AsSplitQuery()
            .SingleAsync(x => x.Id == id, ct);
    }

    public static async Task LockProductsForOrderingAsync(
        IApplicationDbContext db,
        IEnumerable<Guid> productIds,
        CancellationToken ct)
    {
        var ids = productIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        var owners = await db.Products
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.ProductTypeId })
            .ToListAsync(ct);
        foreach (Guid typeId in owners.Select(o => o.ProductTypeId).Distinct().Order())
        {
            await db.ProductTypes
                .Where(t => t.Id == typeId)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.Version, t => t.Version), ct);
        }

        foreach (var owner in owners.OrderBy(o => o.ProductTypeId).ThenBy(o => o.Id))
        {
            await db.Products
                .Where(p => p.Id == owner.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Version, p => p.Version), ct);
        }
    }
}