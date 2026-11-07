using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Ordering.Shared;
using Boilerate.Domain.Messaging;
using Boilerate.Domain.Ordering;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Storefront.Access;

public sealed record RequestGuestEmailCodeRequest(string Email) : IRequest<bool>;

public sealed class RequestGuestEmailCodeRequestValidator : CustomValidator<RequestGuestEmailCodeRequest>
{
}

public sealed class RequestGuestEmailCodeRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    TimeProvider clock,
    IDataProtectionProvider protection) : IRequestHandler<RequestGuestEmailCodeRequest, bool>
{
    public async Task<bool> Handle(RequestGuestEmailCodeRequest request, CancellationToken ct)
    {
        string normalized = OrderingAuthHelper.NormalizeEmail(request.Email);
        DateTime now = clock.GetUtcNow().UtcDateTime;
        string code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var protector = protection.CreateProtector("Boilerate.GuestStorefront.EmailOtp.Outbox.v1");
        string payload = protector.Protect(JsonSerializer.Serialize(new GuestEmailVerificationMessage(request.Email.Trim(), code)));

        return await work.ExecuteAsync(async () =>
        {
            var recent = await db.Set<GuestEmailChallenge>()
                .Where(c => c.NormalizedEmail == normalized && c.CreatedAtUtc > now.AddHours(-1))
                .OrderByDescending(c => c.CreatedAtUtc)
                .ToListAsync(ct);
            if (recent.Count >= 5)
            {
                throw new ConflictException("Please wait before requesting another verification code.");
            }

            if (recent.Count != 0 && recent[0].CreatedAtUtc > now.AddSeconds(-60))
            {
                throw new ConflictException("Please wait before requesting another verification code.");
            }

            if (recent.Count != 0 && recent[0].ConsumedAtUtc is null && recent[0].Attempts < 5)
            {
                recent[0].RecordFailedAttempt();
            }

            var challenge = new GuestEmailChallenge(
                normalized,
                GuestOtpHasher.Hash(normalized, code, now),
                now,
                now.Add(GuestOtpHasher.ChallengeLifetime));
            db.Set<GuestEmailChallenge>().Add(challenge);
            db.OutboxMessages.Add(OutboxMessage.GuestEmailVerification(payload));
            return true;
        }, ct);
    }
}

