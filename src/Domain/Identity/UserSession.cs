namespace Boilerate.Domain.Identity;

/// <summary>One login session. Refresh token plaintext is never persisted.</summary>
public sealed class UserSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FamilyId { get; set; }
    public string UserId { get; set; } = default!;
    public string RefreshTokenHash { get; set; } = default!;
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresOn { get; set; }
    public DateTime? RevokedOn { get; set; }
    public DateTime? ConsumedOn { get; set; }
    public Guid? ReplacedById { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = default!;
}
