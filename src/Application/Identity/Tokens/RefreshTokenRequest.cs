using FluentValidation;
namespace Boilerate.Application.Identity.Tokens;
public sealed class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
public sealed class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(256);
}
