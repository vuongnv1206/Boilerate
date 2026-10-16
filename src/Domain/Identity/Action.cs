using Boilerate.Domain.Common.Contracts;

namespace Boilerate.Domain.Identity;

/// <summary>
/// Action entity (represents an operation)
/// Examples: View, Create, Update, Delete, Export, Import
/// </summary>
public class Action : BaseEntity
{
    public string Code { get; set; } = default!;
    public string NormalizedCode { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public string Version { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>
    /// Action name (e.g., "View", "Create", "Update", "Delete")
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Functions that have this action (many-to-many relationship)
    /// </summary>
    public virtual ICollection<ActionInFunction> ActionInFunctions { get; set; } = default!;

    public Action()
    {
    }

    public Action(string name)
    {
        Name = name;
        Code = name;
        NormalizedCode = name.ToUpperInvariant();
    }
}
