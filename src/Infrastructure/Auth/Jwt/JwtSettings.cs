using System.ComponentModel.DataAnnotations;

namespace Boilerate.Infrastructure.Auth.Jwt;

/// <summary>
/// JWT authentication settings
/// </summary>
public class JwtSettings : IValidatableObject
{
    [Required] public string Issuer { get; set; } = "Boilerate";
    [Required] public string Audience { get; set; } = "Boilerate.Api";
    /// <summary>
    /// Secret key for signing tokens (minimum 32 characters)
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Access token expiration time in minutes
    /// </summary>
    [Range(1, 60)] public int TokenExpirationInMinutes { get; set; } = 15;

    /// <summary>
    /// Refresh token expiration time in days
    /// </summary>
    [Range(1, 90)] public int RefreshTokenExpirationInDays { get; set; } = 30;

    /// <summary>
    /// Validate JWT settings
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrEmpty(Key))
        {
            yield return new ValidationResult(
                "No Key defined in JwtSettings config",
                new[] { nameof(Key) });
        }

        if (System.Text.Encoding.UTF8.GetByteCount(Key) < 32)
        {
            yield return new ValidationResult(
                "JWT Key must be at least 32 characters",
                new[] { nameof(Key) });
        }
    }
}
