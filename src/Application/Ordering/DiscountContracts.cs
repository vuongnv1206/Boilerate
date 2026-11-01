using Boilerate.Application.Catalog;
using Boilerate.Domain.Ordering;

namespace Boilerate.Application.Ordering;

public sealed record SaveDiscount(string Code, string Name, DiscountKind Kind, decimal Value, decimal MinimumSubtotal,
    DateTime? StartsAtUtc, DateTime? EndsAtUtc, int? UsageLimit, bool IsActive, string? Version = null);
public sealed record DiscountDto(Guid Id, string Code, string Name, DiscountKind Kind, decimal Value, decimal MinimumSubtotal,
    DateTime? StartsAtUtc, DateTime? EndsAtUtc, int? UsageLimit, int UsedCount, bool IsActive, string Version);
public sealed record SetCartDiscount(string? Version, string? Code);

public interface IDiscountService
{
    Task<Page<DiscountDto>> ListAsync(int page, int size, string? search, bool? isActive, CancellationToken ct);
    Task<DiscountDto> GetAsync(Guid id, CancellationToken ct);
    Task<DiscountDto> CreateAsync(SaveDiscount request, CancellationToken ct);
    Task<DiscountDto> UpdateAsync(Guid id, SaveDiscount request, CancellationToken ct);
}
