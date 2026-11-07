using System.Security.Cryptography;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Ordering.Shared;
using Boilerate.Domain.Ordering;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Storefront.Access;

public sealed record VerifyGuestEmailCodeRequest(string Email, string Code) : IRequest<string>;

public sealed class VerifyGuestEmailCodeRequestValidator : CustomValidator<VerifyGuestEmailCodeRequest>
{
}

public sealed class VerifyGuestEmailCodeRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    TimeProvider clock) : IRequestHandler<VerifyGuestEmailCodeRequest, string>
{
    public async Task<string> Handle(VerifyGuestEmailCodeRequest request, CancellationToken ct)
    {
        string normalized = OrderingAuthHelper.NormalizeEmail(request.Email);
        if (request.Code is null || request.Code.Length != 6 || request.Code.Any(c => !char.IsAsciiDigit(c)))
        {
            throw new ForbiddenException("The verification code is invalid or expired.");
        }

        DateTime now = clock.GetUtcNow().UtcDateTime;
        bool valid = await work.ExecuteAsync(async () =>
        {
            var challenge = await db.Set<GuestEmailChallenge>()
                .Where(c => c.NormalizedEmail == normalized)
                .OrderByDescending(c => c.CreatedAtUtc)
                .FirstOrDefaultAsync(ct);
            if (challenge is null || challenge.ExpiresAtUtc <= now || challenge.ConsumedAtUtc is not null || challenge.Attempts >= 5)
            {
                return false;
            }

            string actual = GuestOtpHasher.Hash(normalized, request.Code, challenge.CreatedAtUtc);
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(actual), Convert.FromHexString(challenge.CodeHash)))
            {
                challenge.RecordFailedAttempt();
                return false;
            }

            challenge.Consume(now);
            return true;
        }, ct);

        if (!valid)
        {
            throw new ForbiddenException("The verification code is invalid or expired.");
        }

        return normalized;
    }
}

