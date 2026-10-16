using System.Text;
using Boilerate.Domain.Identity;
using FluentValidation;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Infrastructure.Identity;

internal partial class UserService
{
    private async Task<string> GetEmailVerificationUriAsync(ApplicationUser user, string origin)
    {
        string code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        const string route = "api/users/confirm-email/";
        var endpointUri = new Uri(string.Concat($"{origin}/", route));

        string verificationUri = QueryHelpers.AddQueryString(endpointUri.ToString(), "userId", user.Id);
        verificationUri = QueryHelpers.AddQueryString(verificationUri, "code", code);

        return verificationUri;
    }

    public async Task<string> ConfirmEmailAsync(string userId, string code, CancellationToken cancellationToken)
    {
        const string invalidRequest = "Invalid email confirmation request.";
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code))
        {
            throw new ValidationException(invalidRequest);
        }

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        }
        catch (FormatException)
        {
            throw new ValidationException(invalidRequest);
        }

        var user = await _userManager.Users
            .Where(u => u.Id == userId && !u.EmailConfirmed)
            .FirstOrDefaultAsync(cancellationToken);

        _ = user ?? throw new ValidationException(invalidRequest);

        var result = await _userManager.ConfirmEmailAsync(user, token);

        return result.Succeeded
            ? "Email confirmed. Sign in using /api/tokens/get."
            : throw new ValidationException(invalidRequest);
    }

    public async Task<string> ConfirmPhoneNumberAsync(string userId, string code)
    {
        const string invalidRequest = "Invalid phone confirmation request.";
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code))
        {
            throw new ValidationException(invalidRequest);
        }

        var user = await _userManager.FindByIdAsync(userId);

        _ = user ?? throw new ValidationException(invalidRequest);

        if (string.IsNullOrEmpty(user.PhoneNumber))
        {
            throw new ValidationException(invalidRequest);
        }

        var result = await _userManager.ChangePhoneNumberAsync(user, user.PhoneNumber, code);

        return result.Succeeded
            ? "Phone number confirmed."
            : throw new ValidationException(invalidRequest);
    }
}
