using FluentValidation;

namespace Boilerate.Application.Identity.Tokens;

/// <summary>
/// Request để lấy access token (login)
/// </summary>
public record TokenRequest(string Email, string Password);

/// <summary>
/// Validator cho TokenRequest
/// </summary>
public class TokenRequestValidator : AbstractValidator<TokenRequest>
{
    public TokenRequestValidator()
    {
        RuleFor(p => p.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Email is required.")
            .EmailAddress()
            .WithMessage("Invalid Email Address.");

        RuleFor(p => p.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Password is required.");
    }
}
