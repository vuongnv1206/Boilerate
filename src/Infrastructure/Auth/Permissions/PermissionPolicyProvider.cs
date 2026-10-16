using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Boilerate.Infrastructure.Auth.Permissions;

/// <summary>
/// Dynamic Policy Provider
/// Tự động tạo Authorization Policy cho bất kỳ permission string nào bắt đầu bằng "Permissions."
/// </summary>
public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    public DefaultAuthorizationPolicyProvider FallbackPolicyProvider { get; }

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        // Dùng Default provider làm fallback cho các policies truyền thống
        FallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
        FallbackPolicyProvider.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
        FallbackPolicyProvider.GetFallbackPolicyAsync();

    /// <summary>
    /// Tạo policy động dựa trên tên permission (VD: "Permissions.User.View")
    /// </summary>
    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        // Nếu policy bắt đầu bằng "Permissions.", tạo policy động với PermissionRequirement
        if (policyName.StartsWith("Permissions.", StringComparison.OrdinalIgnoreCase))
        {
            var policy = new AuthorizationPolicyBuilder();
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(new PermissionRequirement(policyName));
            return Task.FromResult<AuthorizationPolicy?>(policy.Build());
        }

        // Ngược lại, dùng default policy provider
        return FallbackPolicyProvider.GetPolicyAsync(policyName);
    }
}
