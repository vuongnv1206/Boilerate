using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Ordering;

namespace Boilerate.Application.Ordering.Discounts;

public sealed record SaveDiscount(
    string Code,
    string Name,
    DiscountKind Kind,
    decimal Value,
    decimal MinimumSubtotal,
    DateTime? StartsAtUtc,
    DateTime? EndsAtUtc,
    int? UsageLimit,
    bool IsActive,
    string? Version = null);

public sealed record DiscountDto(
    Guid Id,
    string Code,
    string Name,
    DiscountKind Kind,
    decimal Value,
    decimal MinimumSubtotal,
    DateTime? StartsAtUtc,
    DateTime? EndsAtUtc,
    int? UsageLimit,
    int UsedCount,
    bool IsActive,
    string Version);

internal static class DiscountMapper
{
    internal static DiscountDto Map(Discount d, int count) =>
        new(
            d.Id,
            d.Code,
            d.Name,
            d.Kind,
            d.Value,
            d.MinimumSubtotal,
            CommerceConcurrency.UtcTimestamp(d.StartsAtUtc),
            CommerceConcurrency.UtcTimestamp(d.EndsAtUtc),
            d.UsageLimit,
            count,
            d.IsActive,
            d.Version);
}

