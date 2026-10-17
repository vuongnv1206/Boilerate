using Boilerate.Domain.Common.Contracts;
using Boilerate.Domain.Common.Exceptions;
namespace Boilerate.Domain.Catalog;

// Commerce history uses explicit lifecycle states instead of generic soft deletion.
public abstract class CommerceEntity : BaseEntity, IAuditableEntity
{
    public Guid CreatedBy
    {
        get; set;
    }
    public DateTime CreatedOn
    {
        get; set;
    }
    public Guid LastModifiedBy
    {
        get; set;
    }
    public DateTime? LastModifiedOn
    {
        get; set;
    }
    public string Version { get; private set; } = Guid.NewGuid().ToString("N");
    public void Touch() => Version = Guid.NewGuid().ToString("N");
}
public static class CatalogRules
{
    public static string Required(string? value, int maximum, string field)
    {
        value = value?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > maximum)
            throw new DomainException($"{field} is required and must not exceed {maximum} characters.");
        return value;
    }
    public static string Code(string? value, int maximum = 100)
    {
        value = Required(value, maximum, "Code");
        if (value.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.'))
            throw new DomainException("Codes must contain ASCII letters, numbers, '.', '-' or '_'.");
        return value;
    }
    public static string Slug(string? value)
    {
        value = Required(value, 200, "Slug").ToLowerInvariant();
        if (value.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            throw new DomainException("Slug must contain ASCII letters, numbers or '-'.");
        return value;
    }
    public static void Money(decimal? price, decimal? comparison)
    {
        if (price is < 0 or > 9999999999999999m || comparison is < 0 or > 9999999999999999m ||
            (price.HasValue && decimal.Truncate(price.Value) != price) ||
            (comparison.HasValue && (decimal.Truncate(comparison.Value) != comparison || !price.HasValue || comparison <= price)))
            throw new DomainException("VND prices must be whole nonnegative amounts; compare-at price must exceed price.");
    }
    public static bool IsFieldNumber(decimal value) => value > -100000000000000m && value < 100000000000000m && decimal.Round(value, 4) == value;
}
public enum ProductStatus
{
    Draft = 1, Published = 2, Archived = 3
}
public enum ProductFulfillmentKind
{
    Physical = 1, DigitalUnique = 2, DigitalShared = 3, ManualExtension = 4
}
public enum ProductFieldType
{
    Text = 1, Number = 2, Boolean = 3, Date = 4, Choice = 5
}
