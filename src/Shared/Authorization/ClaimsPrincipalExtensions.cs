using System.Security.Claims;
using Boilerate.Shared.Authorization;

namespace Boilerate.Shared.Authorization;

/// <summary>
/// Extension methods cho ClaimsPrincipal.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Lấy Email từ ClaimTypes.Email.
    /// </summary>
    public static string? GetEmail(this ClaimsPrincipal principal)
        => principal.FindFirstValue(ClaimTypes.Email);

    /// <summary>
    /// Lấy Full Name từ AppClaims.Fullname.
    /// </summary>
    public static string? GetFullName(this ClaimsPrincipal principal)
        => principal?.FindFirst(AppClaims.Fullname)?.Value;

    /// <summary>
    /// Lấy First Name từ ClaimTypes.Name.
    /// </summary>
    public static string? GetFirstName(this ClaimsPrincipal principal)
        => principal?.FindFirst(ClaimTypes.Name)?.Value;

    /// <summary>
    /// Lấy Surname từ ClaimTypes.Surname.
    /// </summary>
    public static string? GetSurname(this ClaimsPrincipal principal)
        => principal?.FindFirst(ClaimTypes.Surname)?.Value;

    /// <summary>
    /// Lấy Phone Number từ ClaimTypes.MobilePhone.
    /// </summary>
    public static string? GetPhoneNumber(this ClaimsPrincipal principal)
        => principal.FindFirstValue(ClaimTypes.MobilePhone);

    /// <summary>
    /// Lấy User ID từ ClaimTypes.NameIdentifier.
    /// </summary>
    public static string? GetUserId(this ClaimsPrincipal principal)
        => principal.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>
    /// Lấy Image URL từ AppClaims.ImageUrl.
    /// </summary>
    public static string? GetImageUrl(this ClaimsPrincipal principal)
        => principal.FindFirstValue(AppClaims.ImageUrl);

    /// <summary>
    /// Lấy Token Expiration từ AppClaims.Expiration.
    /// </summary>
    public static DateTimeOffset GetExpiration(this ClaimsPrincipal principal) =>
        DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(
            principal.FindFirstValue(AppClaims.Expiration),
            null));

    /// <summary>
    /// Helper method để tìm claim value.
    /// </summary>
    private static string? FindFirstValue(this ClaimsPrincipal principal, string claimType) =>
        principal is null
            ? throw new ArgumentNullException(nameof(principal))
            : principal.FindFirst(claimType)?.Value;
}
