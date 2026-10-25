using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Ordering;
using Boilerate.Domain.Messaging;
using Boilerate.Domain.Ordering;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Infrastructure.Commerce;

public sealed class GuestAccessService(ApplicationDbContext db, CommerceUnitOfWork work, TimeProvider clock,
    IDataProtectionProvider protection) : IGuestAccessService
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    private static string Hash(string email, string code, DateTime createdAtUtc) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes($"{email}|{code}|{createdAtUtc.Ticks}")));

    public async Task RequestCodeAsync(string email, CancellationToken ct)
    {
        var normalized = OrderingService.NormalizeEmail(email);
        var now = clock.GetUtcNow().UtcDateTime;
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        var protector = protection.CreateProtector("Boilerate.GuestStorefront.EmailOtp.Outbox.v1");
        var payload = protector.Protect(JsonSerializer.Serialize(new GuestEmailVerificationMessage(email.Trim(), code)));

        await work.ExecuteAsync(async () =>
        {
            var recent = await db.Set<GuestEmailChallenge>().Where(c => c.NormalizedEmail == normalized && c.CreatedAtUtc > now.AddHours(-1))
                .OrderByDescending(c => c.CreatedAtUtc).ToListAsync(ct);
            if (recent.Count >= 5)
                throw new ConflictException("Please wait before requesting another verification code.");
            if (recent.Count != 0 && recent[0].CreatedAtUtc > now.AddSeconds(-60))
                throw new ConflictException("Please wait before requesting another verification code.");
            // Only the newest code is valid. Five failed attempts also invalidate it.
            if (recent.Count != 0 && recent[0].ConsumedAtUtc is null && recent[0].Attempts < 5)
                recent[0].RecordFailedAttempt();
            var challenge = new GuestEmailChallenge(normalized, Hash(normalized, code, now), now, now.Add(Lifetime));
            db.Set<GuestEmailChallenge>().Add(challenge);
            db.OutboxMessages.Add(OutboxMessage.GuestEmailVerification(payload));
            return true;
        }, ct);
    }

    public async Task VerifyCodeAsync(string email, string code, CancellationToken ct)
    {
        var normalized = OrderingService.NormalizeEmail(email);
        if (code is null || code.Length != 6 || code.Any(c => !char.IsAsciiDigit(c)))
            throw new ForbiddenException("The verification code is invalid or expired.");
        var now = clock.GetUtcNow().UtcDateTime;
        var valid = await work.ExecuteAsync(async () =>
        {
            var challenge = await db.Set<GuestEmailChallenge>().Where(c => c.NormalizedEmail == normalized)
                .OrderByDescending(c => c.CreatedAtUtc).FirstOrDefaultAsync(ct);
            if (challenge is null || challenge.ExpiresAtUtc <= now || challenge.ConsumedAtUtc is not null || challenge.Attempts >= 5)
                return false;
            var actual = Hash(normalized, code, challenge.CreatedAtUtc);
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(actual), Convert.FromHexString(challenge.CodeHash)))
            {
                challenge.RecordFailedAttempt();
                return false;
            }
            challenge.Consume(now);
            return true;
        }, ct);
        if (!valid) throw new ForbiddenException("The verification code is invalid or expired.");
    }
}

public sealed class StorefrontSettingsService(ApplicationDbContext db, CommerceUnitOfWork work) : IStorefrontSettingsService
{
    public async Task<GuestPaymentSettingsDto> GetPublicAsync(CancellationToken ct)
    {
        var settings = await db.Set<StorefrontPaymentSettings>().AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == StorefrontPaymentSettings.SingletonId, ct);
        return settings is null || !settings.BankTransferEnabled ? new(null, null, null, null, null, false) : Public(settings);
    }

    public async Task<AdminStorefrontSettingsDto> GetAdminAsync(CancellationToken ct)
    {
        return await work.ExecuteAsync(async () =>
        {
            var settings = await db.Set<StorefrontPaymentSettings>().SingleOrDefaultAsync(s => s.Id == StorefrontPaymentSettings.SingletonId, ct);
            if (settings is null)
            {
                settings = new StorefrontPaymentSettings(StorefrontPaymentSettings.SingletonId);
                db.Add(settings);
            }
            return Admin(settings);
        }, ct);
    }

    public async Task<AdminStorefrontSettingsDto> UpdateAsync(UpdateStorefrontSettings request, CancellationToken ct)
    {
        return await work.ExecuteAsync(async () =>
        {
            var settings = await db.Set<StorefrontPaymentSettings>().SingleOrDefaultAsync(s => s.Id == StorefrontPaymentSettings.SingletonId, ct);
            if (settings is null)
            {
                if (!string.IsNullOrEmpty(request.Version)) throw new ConflictException("Settings changed; reload before editing.");
                settings = new StorefrontPaymentSettings(StorefrontPaymentSettings.SingletonId);
                db.Add(settings);
            }
            else if (string.IsNullOrWhiteSpace(request.Version) || request.Version != settings.Version)
                throw new ConflictException("Settings changed; reload before editing.");
            settings.Update(request.BankName, request.AccountNumber, request.AccountHolder, request.Branch, request.Instructions, request.BankTransferEnabled);
            return Admin(settings);
        }, ct);
    }

    private static GuestPaymentSettingsDto Public(StorefrontPaymentSettings s) => new(
        s.BankName, s.AccountNumber, s.AccountHolder, s.Branch, s.Instructions, s.BankTransferEnabled);
    private static AdminStorefrontSettingsDto Admin(StorefrontPaymentSettings s) => new(
        s.BankName, s.AccountNumber, s.AccountHolder, s.Branch, s.Instructions, s.BankTransferEnabled, s.Version);
}
