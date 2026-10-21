using Microsoft.EntityFrameworkCore;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Mailing;
using Boilerate.Application.Identity.Users;
using Boilerate.Domain.Common;
using Boilerate.Domain.Identity;
using Boilerate.Shared.Authorization;

namespace Boilerate.Infrastructure.Identity;

internal partial class UserService
{
    public async Task<string> CreateAsync(CreateUserRequest request, string origin)
    {
        if (_actor.IsAuthenticated()) await _guard.RequireAdminAsync(default);
        else if (!_securitySettings.AllowSelfRegistration) throw new ForbiddenException("Self-registration is disabled.");
        await RequestValidation.ValidateAsync(_services, request);
        origin = _securitySettings.PublicBaseUrl;
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var user = new ApplicationUser
        {
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            UserName = request.UserName,
            PhoneNumber = request.PhoneNumber,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            throw new ConflictException(string.Join("; ", result.GetErrors()));
        }

        var roleResult = await _userManager.AddToRoleAsync(user, AppRoles.Basic);
        if (!roleResult.Succeeded) throw new ConflictException(string.Join("; ", roleResult.GetErrors()));

        _guard.Audit("Users", user.Id, null, new { user.UserName, user.Email, user.IsActive });
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        var messages = new List<string> { string.Format("User {0} Registered.", user.UserName) };

        if (_securitySettings.RequireConfirmedAccount && !string.IsNullOrEmpty(user.Email))
        {
            string emailVerificationUri = await GetEmailVerificationUriAsync(user, origin);
            RegisterUserEmailModel eMailModel = new RegisterUserEmailModel()
            {
                Email = user.Email,
                UserName = user.UserName,
                Url = emailVerificationUri
            };
            var mailRequest = new MailRequest(
                new List<string> { user.Email },
                "Confirm Registration",
                _templateService.GenerateEmailTemplate("email-confirmation", eMailModel));

            _jobService.Enqueue(() => _mailService.SendAsync(mailRequest, CancellationToken.None));
            messages.Add($"Please check {user.Email} to verify your account!");
        }

        return string.Join(Environment.NewLine, messages);
    }

    public async Task UpdateAsync(UpdateUserRequest request, string userId)
    {
        request.Id = userId;
        await RequestValidation.ValidateAsync(_services, request);
        var user = await _userManager.FindByIdAsync(userId);

        _ = user ?? throw new NotFoundException("User Not Found.");

        if (!string.Equals(request.Email, user.Email, StringComparison.OrdinalIgnoreCase))
            throw new FluentValidation.ValidationException("Profile email changes require a separate verification flow.");
        var currentImage = user.ImageUrl;
        string? uploadedImage = null;
        if (request.Image is not null)
            uploadedImage = await _fileStorage.UploadAsync<ApplicationUser>(request.Image, FileType.Image);
        try
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();
            await LockAccountAsync(user.Id);
            await _db.Entry(user).ReloadAsync();
            if (!string.IsNullOrWhiteSpace(request.Version))
                AclGuard.CheckVersion(request.Version, user.ConcurrencyStamp);
            var previousPhone = user.PhoneNumber;
            user.FirstName = request.FirstName;
            user.LastName = request.LastName;
            if (uploadedImage is not null || request.DeleteCurrentImage)
                user.ImageUrl = uploadedImage ?? string.Empty;
            if (request.PhoneNumber != previousPhone)
            {
                var phoneResult = await _userManager.SetPhoneNumberAsync(user, request.PhoneNumber);
                if (!phoneResult.Succeeded) throw new ConflictException(string.Join("; ", phoneResult.GetErrors()));
            }
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded) throw new ConflictException(string.Join("; ", result.GetErrors()));
            await transaction.CommitAsync();
        }
        catch
        {
            if (uploadedImage is not null) _fileStorage.Remove(uploadedImage);
            throw;
        }
        // Remove the old file only after the database points to its replacement.
        if ((uploadedImage is not null || request.DeleteCurrentImage) && !string.IsNullOrEmpty(currentImage))
            _fileStorage.Remove(currentImage);
    }
}
