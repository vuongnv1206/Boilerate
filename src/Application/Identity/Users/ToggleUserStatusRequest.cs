namespace Boilerate.Application.Identity.Users;

public class ToggleUserStatusRequest
{
    public string Version { get; set; } = string.Empty;
    public string UserId { get; set; } = default!;
    public bool ActivateUser { get; set; }
}
