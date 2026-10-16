namespace Boilerate.Application.Identity.Roles;

/// <summary>
/// Action DTO (represents an operation)
/// </summary>
public class ActionDto
{
    public string Code { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    /// <summary>
    /// Action ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Action name (e.g., "View", "Create", "Update", "Delete")
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Is this action selected for current role (checkbox state)
    /// </summary>
    public bool Selected { get; set; }
}
