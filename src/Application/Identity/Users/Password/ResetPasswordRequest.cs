using FluentValidation;
namespace Boilerate.Application.Identity.Users.Password;

public class ResetPasswordRequest
{
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? Token { get; set; }
}

public sealed class ResetPasswordRequestValidator : FluentValidation.AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(9);
        RuleFor(x => x.Token).NotEmpty();
    }
}
