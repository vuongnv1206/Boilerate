namespace Boilerate.Application.Identity.Roles;

/// <summary>
/// Function DTO (represents a module/feature)
/// </summary>
public class FunctionDto
{
    public Guid GroupId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    /// <summary>
    /// Function ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Function name (e.g., "Users", "Products", "Orders")
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// List of actions available for this function
    /// </summary>
    public List<ActionDto> ActionDtos { get; set; } = default!;
}
