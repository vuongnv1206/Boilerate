namespace Boilerate.Application.Identity.Tokens;
public record TokenResponse(string Token, string RefreshToken, DateTime RefreshTokenExpiryTime,
    Guid SessionId, DateTime AccessTokenExpiresAt);
public record SessionDto(Guid Id, DateTime CreatedOn, DateTime ExpiresOn, string IpAddress);
