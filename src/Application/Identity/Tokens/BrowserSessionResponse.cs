namespace Boilerate.Application.Identity.Tokens;

public sealed record BrowserSessionResponse(string Token, Guid SessionId, DateTime AccessTokenExpiresAt, string UserId);
