using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Contracts;
using Boilerate.Domain.Common.Exceptions;

namespace Boilerate.Domain.Ordering;

public enum DiscountKind { Percentage = 1, Fixed = 2 }

public sealed class Discount : CommerceEntity, IAggregateRoot
{
    public const decimal MaxSafeMoney = 9007199254740991m;
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public DiscountKind Kind { get; private set; }
    public decimal Value { get; private set; }
    public decimal MinimumSubtotal { get; private set; }
    public DateTime? StartsAtUtc { get; private set; }
    public DateTime? EndsAtUtc { get; private set; }
    public int? UsageLimit { get; private set; }
    public bool IsActive { get; private set; }

    private Discount() { }

    public Discount(string code, string name, DiscountKind kind, decimal value, decimal minimumSubtotal,
        DateTime? startsAtUtc, DateTime? endsAtUtc, int? usageLimit, bool isActive)
        => Update(code, name, kind, value, minimumSubtotal, startsAtUtc, endsAtUtc, usageLimit, isActive);

    public static string NormalizeCode(string code) => CatalogRules.Code(code, 32).ToUpperInvariant();

    public void Update(string code, string name, DiscountKind kind, decimal value, decimal minimumSubtotal,
        DateTime? startsAtUtc, DateTime? endsAtUtc, int? usageLimit, bool isActive)
    {
        code = NormalizeCode(code);
        name = CatalogRules.Required(name, 160, "Name");
        if (!Enum.IsDefined(kind) || value != decimal.Truncate(value) || value < 1 ||
            (kind == DiscountKind.Percentage && value > 99) || (kind == DiscountKind.Fixed && value > MaxSafeMoney) ||
            minimumSubtotal < 0 || minimumSubtotal > MaxSafeMoney || minimumSubtotal != decimal.Truncate(minimumSubtotal) ||
            usageLimit is <= 0 || (startsAtUtc.HasValue && startsAtUtc.Value.Kind != DateTimeKind.Utc) ||
            (endsAtUtc.HasValue && endsAtUtc.Value.Kind != DateTimeKind.Utc) ||
            (startsAtUtc.HasValue && endsAtUtc.HasValue && startsAtUtc >= endsAtUtc))
            throw new DomainException("Discount values, usage limit or UTC validity period are invalid.");
        Code = code; Name = name; Kind = kind; Value = value; MinimumSubtotal = minimumSubtotal;
        StartsAtUtc = startsAtUtc; EndsAtUtc = endsAtUtc; UsageLimit = usageLimit; IsActive = isActive;
        Touch();
    }

    public const string NotApplicable = "Mã giảm giá không áp dụng được.";

    public (decimal Amount, string? Error) Quote(decimal subtotal, DateTime now, int usedCount)
    {
        // One answer for every code that cannot be used right now, so guessing reveals nothing about which codes exist.
        if (!IsActive ||
            (StartsAtUtc.HasValue && now < StartsAtUtc) ||
            (EndsAtUtc.HasValue && now >= EndsAtUtc) ||
            (UsageLimit.HasValue && usedCount >= UsageLimit))
        {
            return (0, NotApplicable);
        }

        if (subtotal < MinimumSubtotal) return (0, "Đơn hàng chưa đạt giá trị tối thiểu của mã.");
        var amount = Kind == DiscountKind.Percentage ? decimal.Floor(subtotal * Value / 100m) : Value;
        if (amount <= 0 || amount >= subtotal) return (0, "Mã giảm giá không phù hợp với tiền hàng hiện tại.");
        return (amount, null);
    }
}
