using Microsoft.EntityFrameworkCore;

namespace Boilerate.Domain.Identity;

/// <summary>
/// Bảng giao (junction) giữa Action và Function.
/// Mỗi Function có thể chứa nhiều Actions (VD: Users module có View, Create, Update, Delete).
/// Sử dụng composite key (ActionId, FunctionId).
/// </summary>
[PrimaryKey(nameof(ActionId), nameof(FunctionId))]
public class ActionInFunction
{
    public bool IsActive { get; set; } = true;
    /// <summary>
    /// Action ID (foreign key)
    /// </summary>
    public Guid ActionId { get; set; }

    /// <summary>
    /// Function ID (foreign key)
    /// </summary>
    public Guid FunctionId { get; set; }

    /// <summary>
    /// Navigation property to Action
    /// </summary>
    public virtual Action Action { get; set; } = default!;

    /// <summary>
    /// Navigation property to Function
    /// </summary>
    public virtual Function Function { get; set; } = default!;

    public ActionInFunction()
    {
    }

    public ActionInFunction(Guid actionId, Guid functionId)
    {
        ActionId = actionId;
        FunctionId = functionId;
    }
}
