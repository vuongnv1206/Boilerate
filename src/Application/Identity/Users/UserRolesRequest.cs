namespace Boilerate.Application.Identity.Users;
public sealed class UserRolesRequest
{
    public string Version { get; set; } = string.Empty;
    public List<string> RoleIds { get; set; } = new();
}
