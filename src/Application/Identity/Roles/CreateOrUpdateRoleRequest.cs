using FluentValidation;

namespace Boilerate.Application.Identity.Roles;

/// <summary>
/// Request để tạo hoặc update role
/// </summary>
public class CreateOrUpdateRoleRequest
{
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Role ID (null = create, not null = update)
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Role name (required, unique)
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Role description (optional)
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Validator cho CreateOrUpdateRoleRequest
/// </summary>
public class CreateOrUpdateRoleRequestValidator : AbstractValidator<CreateOrUpdateRoleRequest>
{
    public CreateOrUpdateRoleRequestValidator(IRoleService roleService)
    {
        RuleFor(r => r.Name).Cascade(CascadeMode.Stop)
            .NotEmpty().MaximumLength(256)
            .WithMessage("Role name is required.")
            .MustAsync(async (role, name, _) => !await roleService.ExistsAsync(name, role.Id))
            .WithMessage("Similar Role already exists.");
    }
}
