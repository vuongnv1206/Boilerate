namespace Boilerate.Application.Identity.Users;

public sealed record AuthorizationContextDto(List<string> Permissions, bool IsCentralAdmin);
