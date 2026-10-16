namespace Boilerate.Application.Identity.Users;

/// <summary>
/// User role DTO (for assign roles UI)
/// </summary>
public class UserRoleDto
{
    /// <summary>
    /// Role ID
    /// </summary>
    public string RoleId { get; set; } = default!;

    /// <summary>
    /// Role name
    /// </summary>
    public string RoleName { get; set; } = default!;

    /// <summary>
    /// Role description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Is this role assigned to user (checkbox state)
    /// </summary>
    public bool Enabled { get; set; }
}
