using System.Text.Json.Serialization;

namespace Boilerate.Application.Identity.Tokens;
public record TokenResponse(string Token, string RefreshToken, DateTime RefreshTokenExpiryTime,
    Guid SessionId, DateTime AccessTokenExpiresAt)
{
    [JsonIgnore]
    public string UserId { get; init; } = string.Empty;
}
public record SessionDto(Guid Id, DateTime CreatedOn, DateTime ExpiresOn, string IpAddress);
