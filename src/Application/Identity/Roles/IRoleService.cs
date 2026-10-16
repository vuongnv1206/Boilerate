using Boilerate.Application.Common.Interfaces;

namespace Boilerate.Application.Identity.Roles;

/// <summary>
/// Service xử lý role management operations
/// </summary>
public interface IRoleService : ITransientService
{
    /// <summary>
    /// Get list tất cả roles
    /// </summary>
    Task<List<RoleDto>> GetListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Get total role count
    /// </summary>
    Task<int> GetCountAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Check role name đã tồn tại chưa (exclude excludeId nếu có)
    /// </summary>
    Task<bool> ExistsAsync(string roleName, string? excludeId);

    /// <summary>
    /// Get role details by ID
    /// </summary>
    Task<RoleDto> GetByIdAsync(string id);

    /// <summary>
    /// Get role details vá»›i permissions (Functions + Actions)
    /// Returns list of Functions with Actions marked as Selected or not
    /// </summary>
    Task<List<FunctionDto>> GetByIdWithPermissionsAsync(string roleId, CancellationToken cancellationToken);

    /// <summary>
    /// Create hoặc update role
    /// </summary>
    Task<string> CreateOrUpdateAsync(CreateOrUpdateRoleRequest request);

    /// <summary>
    /// Update permissions cá»§a role (table-based approach)
    /// Replaces all current permissions with new ones
    /// </summary>
    Task<string> UpdatePermissionsAsync(
        UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Delete role vá»›i validation
    /// Cannot delete default roles (Admin, Basic)
    /// Cannot delete roles đang được users sử dụng
    /// </summary>
    Task<string> DeleteAsync(string id, string version);
}
