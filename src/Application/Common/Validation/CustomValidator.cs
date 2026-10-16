using FluentValidation;

namespace Boilerate.Application.Common.Validation;

/// <summary>
/// Base validator class với common validation rules
/// </summary>
/// <typeparam name="T">Type của object cần validate</typeparam>
public abstract class CustomValidator<T> : AbstractValidator<T>
{
    /// <summary>
    /// Validate GUID không empty
    /// </summary>
    protected IRuleBuilderOptions<T, Guid> MustNotBeEmpty(IRuleBuilder<T, Guid> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty()
            .WithMessage("{PropertyName} is required.");
    }

    /// <summary>
    /// Validate string không empty và max length
    /// </summary>
    protected IRuleBuilderOptions<T, string> MustNotBeEmpty(
        IRuleBuilder<T, string> ruleBuilder,
        int maxLength = 255)
    {
        return ruleBuilder
            .NotEmpty()
            .WithMessage("{PropertyName} is required.")
            .MaximumLength(maxLength)
            .WithMessage("{PropertyName} must not exceed {MaxLength} characters.");
    }

    /// <summary>
    /// Validate email format
    /// </summary>
    protected IRuleBuilderOptions<T, string> MustBeValidEmail(IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty()
            .WithMessage("Email is required.")
            .EmailAddress()
            .WithMessage("Invalid email format.")
            .MaximumLength(255)
            .WithMessage("Email must not exceed 255 characters.");
    }

    /// <summary>
    /// Validate phone number format (E.164)
    /// </summary>
    protected IRuleBuilderOptions<T, string?> MustBeValidPhoneNumber(IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder
            .Matches(@"^\+?[1-9]\d{1,14}$")
            .WithMessage("Invalid phone number format.");
    }

    /// <summary>
    /// Validate password strength
    /// </summary>
    protected IRuleBuilderOptions<T, string> MustBeStrongPassword(IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one number.")
            .Matches("[\\W_]").WithMessage("Password must contain at least one special character.");
    }

    /// <summary>
    /// Validate decimal greater than zero
    /// </summary>
    protected IRuleBuilderOptions<T, decimal> MustBeGreaterThanZero(IRuleBuilder<T, decimal> ruleBuilder)
    {
        return ruleBuilder
            .GreaterThan(0)
            .WithMessage("{PropertyName} must be greater than 0.");
    }

    /// <summary>
    /// Validate int greater than or equal to zero
    /// </summary>
    protected IRuleBuilderOptions<T, int> MustNotBeNegative(IRuleBuilder<T, int> ruleBuilder)
    {
        return ruleBuilder
            .GreaterThanOrEqualTo(0)
            .WithMessage("{PropertyName} cannot be negative.");
    }
}
