using System.ComponentModel.DataAnnotations;
using Boilerate.Domain.Common.Contracts;

namespace Boilerate.Domain.Identity;

/// <summary>
/// Phân quyền: Gắn một Action trong một Function cho một Role cụ thể.
/// VD: Role "Admin" có quyền "Delete" trong module "Users" -> Permission(AdminRoleId, UsersId, DeleteId).
/// </summary>
public class Permission : AuditableEntity, IAggregateRoot
{
    public string RoleId { get; set; } = default!;
    public Guid FunctionId { get; set; }
    public Guid ActionId { get; set; }

    public virtual ApplicationRole Role { get; set; } = default!;
    public virtual Function Function { get; set; } = default!;
    public virtual Action Action { get; set; } = default!;

    public Permission() { }

    public Permission(string roleId, Guid functionId, Guid actionId)
    {
        RoleId = roleId;
        FunctionId = functionId;
        ActionId = actionId;
    }
}
