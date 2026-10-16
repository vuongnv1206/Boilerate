using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Mailing;
using Boilerate.Application.Identity.Users.Password;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Infrastructure.Identity;

internal partial class UserService
{
    public async Task<string> ForgotPasswordAsync(ForgotPasswordRequest request, string origin)
    {
        await RequestValidation.ValidateAsync(_services, request);
        if (!_securitySettings.PasswordRecoveryEnabled) throw new ForbiddenException("Password recovery is disabled.");
        origin = _securitySettings.PublicBaseUrl;
        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user is null || !await _userManager.IsEmailConfirmedAsync(user))
        {
            // Don't reveal that the user does not exist or is not confirmed
            return "If your email is registered, you will receive a password reset link shortly.";
        }

        string code = await _userManager.GeneratePasswordResetTokenAsync(user);

        // Link for frontend: {origin}/account/reset-password?Token=...
        const string route = "account/reset-password";
        var endpointUri = new Uri(string.Concat($"{origin}/", route));
        string passwordResetUrl = QueryHelpers.AddQueryString(endpointUri.ToString(), "Token", code);

        var mailRequest = new MailRequest(
            new List<string> { request.Email },
            "Reset Password",
            $"Your Password Reset Token is '{code}'. You can reset your password using the following link: {passwordResetUrl}");

        _jobService.Enqueue(() => _mailService.SendAsync(mailRequest, CancellationToken.None));

        return "If your email is registered, you will receive a password reset link shortly.";
    }

    public async Task<string> ResetPasswordAsync(ResetPasswordRequest request)
    {
        if (!_securitySettings.PasswordRecoveryEnabled) throw new ForbiddenException("Password recovery is disabled.");
        await RequestValidation.ValidateAsync(_services, request);
        var user = await _userManager.FindByEmailAsync(request.Email!);

        _ = user ?? throw new FluentValidation.ValidationException("Invalid password reset request.");

        await using var transaction = await _db.Database.BeginTransactionAsync();
        await LockAccountAsync(user.Id);
        await _db.Entry(user).ReloadAsync();
        var result = await _userManager.ResetPasswordAsync(user, request.Token!, request.Password!);

        if (!result.Succeeded) throw new ConflictException("Invalid password reset request.");
        await _db.UserSessions.Where(x => x.UserId == user.Id && x.RevokedOn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedOn, DateTime.UtcNow));
        await transaction.CommitAsync();
        return "Password reset successful.";
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest model, string userId)
    {
        await RequestValidation.ValidateAsync(_services, model);
        var user = await _userManager.FindByIdAsync(userId);

        _ = user ?? throw new NotFoundException("User Not Found.");

        await using var transaction = await _db.Database.BeginTransactionAsync();
        await LockAccountAsync(user.Id);
        await _db.Entry(user).ReloadAsync();
        var result = await _userManager.ChangePasswordAsync(user, model.Password, model.NewPassword);

        if (!result.Succeeded)
        {
            throw new ConflictException(string.Join("; ", result.GetErrors()));
        }
        await _db.UserSessions.Where(x => x.UserId == user.Id && x.RevokedOn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedOn, DateTime.UtcNow));
        await transaction.CommitAsync();
    }

    private async Task LockAccountAsync(string id)
    {
        if (await _db.Users.Where(x => x.Id == id).ExecuteUpdateAsync(s =>
                s.SetProperty(x => x.ConcurrencyStamp, x => x.ConcurrencyStamp)) != 1)
            throw new NotFoundException("User not found.");
    }
}
