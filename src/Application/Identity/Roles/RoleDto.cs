namespace Boilerate.Application.Identity.Roles;

/// <summary>
/// Role detail DTO (dùng cho responses)
/// </summary>
public class RoleDto
{
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Role ID (string - Identity framework)
    /// </summary>
    public string Id { get; set; } = default!;

    /// <summary>
    /// Role name (unique)
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Role description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// List of permission strings (optional, for quick display)
    /// Format: "Function.Action" (e.g., "Users.View", "Products.Create")
    /// </summary>
    public List<string>? Permissions { get; set; }
}
