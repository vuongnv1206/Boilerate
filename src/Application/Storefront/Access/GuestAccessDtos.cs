using System.Security.Cryptography;
using System.Text;

namespace Boilerate.Application.Storefront.Access;

public sealed record GuestAccessRequest(string Email);

public sealed record GuestAccessVerification(string Email, string Code);

public sealed record GuestAccessResult(string Email);

internal static class GuestOtpHasher
{
    internal static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(10);

    internal static string Hash(string email, string code, DateTime createdAtUtc) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{email}|{code}|{createdAtUtc.Ticks}")));
}

