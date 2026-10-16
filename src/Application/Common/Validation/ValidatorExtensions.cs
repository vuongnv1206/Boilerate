using FluentValidation;

namespace Boilerate.Application.Common.Validation;

/// <summary>
/// Extension methods cho custom validation rules
/// </summary>
public static class ValidatorExtensions
{
    /// <summary>
    /// Validate list không empty
    /// </summary>
    public static IRuleBuilderOptions<T, IList<TElement>> NotEmptyList<T, TElement>(
        this IRuleBuilder<T, IList<TElement>> ruleBuilder)
    {
        return ruleBuilder
            .NotNull()
            .WithMessage("{PropertyName} is required.")
            .Must(list => list != null && list.Any())
            .WithMessage("{PropertyName} must contain at least one item.");
    }

    /// <summary>
    /// Validate list max count
    /// </summary>
    public static IRuleBuilderOptions<T, IList<TElement>> MaximumCount<T, TElement>(
        this IRuleBuilder<T, IList<TElement>> ruleBuilder,
        int max)
    {
        return ruleBuilder
            .Must(list => list == null || list.Count <= max)
            .WithMessage($"{{PropertyName}} must not exceed {max} items.");
    }

    /// <summary>
    /// Validate date không trong quá khứ
    /// </summary>
    public static IRuleBuilderOptions<T, DateTime> NotInThePast<T>(
        this IRuleBuilder<T, DateTime> ruleBuilder)
    {
        return ruleBuilder
            .Must(date => date >= DateTime.UtcNow)
            .WithMessage("{PropertyName} must not be in the past.");
    }

    /// <summary>
    /// Validate date không trong tương lai
    /// </summary>
    public static IRuleBuilderOptions<T, DateTime> NotInTheFuture<T>(
        this IRuleBuilder<T, DateTime> ruleBuilder)
    {
        return ruleBuilder
            .Must(date => date <= DateTime.UtcNow)
            .WithMessage("{PropertyName} must not be in the future.");
    }

    /// <summary>
    /// Validate date range
    /// </summary>
    public static IRuleBuilderOptions<T, DateTime> WithinRange<T>(
        this IRuleBuilder<T, DateTime> ruleBuilder,
        DateTime min,
        DateTime max)
    {
        return ruleBuilder
            .Must(date => date >= min && date <= max)
            .WithMessage($"{{PropertyName}} must be between {min:yyyy-MM-dd} and {max:yyyy-MM-dd}.");
    }

    /// <summary>
    /// Validate URL format
    /// </summary>
    public static IRuleBuilderOptions<T, string> MustBeValidUrl<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Must(url => string.IsNullOrEmpty(url) || Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("{PropertyName} must be a valid URL.");
    }

    /// <summary>
    /// Validate file extension
    /// </summary>
    public static IRuleBuilderOptions<T, string> HasValidExtension<T>(
        this IRuleBuilder<T, string> ruleBuilder,
        params string[] allowedExtensions)
    {
        return ruleBuilder
            .Must(fileName =>
            {
                if (string.IsNullOrEmpty(fileName)) return true; // Allow null/empty if not required
                var extension = Path.GetExtension(fileName).ToLowerInvariant();
                return allowedExtensions.Contains(extension);
            })
            .WithMessage($"{{PropertyName}} must have one of the following extensions: {string.Join(", ", allowedExtensions)}");
    }

    /// <summary>
    /// Validate unique items trong list
    /// </summary>
    public static IRuleBuilderOptions<T, IList<TElement>> MustHaveUniqueItems<T, TElement>(
        this IRuleBuilder<T, IList<TElement>> ruleBuilder)
    {
        return ruleBuilder
            .Must(list => list == null || list.Distinct().Count() == list.Count)
            .WithMessage("{PropertyName} must not contain duplicate items.");
    }
}
