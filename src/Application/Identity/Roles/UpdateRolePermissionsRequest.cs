using FluentValidation;

namespace Boilerate.Application.Identity.Roles;

/// <summary>
/// Request để update permissions của role (table-based approach)
/// </summary>
public class UpdateRolePermissionsRequest
{
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Role ID (required)
    /// </summary>
    // The HTTP route supplies this before service validation; omitted body IDs must bind.
    public string RoleId { get; set; } = string.Empty;

    /// <summary>
    /// List of permissions (Function + Action combinations)
    /// </summary>
    public List<PermissionRequest> Permissions { get; set; } = default!;
}

/// <summary>
/// Permission request (Function + Action combination)
/// Represents a row in Permission table
/// </summary>
public class PermissionRequest
{
    /// <summary>
    /// Function ID (e.g., Users, Products, Orders)
    /// </summary>
    public Guid FunctionId { get; set; } = default!;

    /// <summary>
    /// Action ID (e.g., View, Create, Update, Delete)
    /// </summary>
    public Guid ActionId { get; set; } = default!;
}

/// <summary>
/// Validator cho UpdateRolePermissionsRequest
/// </summary>
public class UpdateRolePermissionsRequestValidator : AbstractValidator<UpdateRolePermissionsRequest>
{
    public UpdateRolePermissionsRequestValidator()
    {
        RuleFor(r => r.RoleId)
            .NotEmpty()
            .WithMessage("Role ID is required.");

        RuleFor(r => r.Permissions)
            .NotNull()
            .WithMessage("Permissions list is required.");
    }
}
