using System.Security.Claims;

namespace Boilerate.Infrastructure.Auth;

/// <summary>
/// Interface để initialize current user (dùng trong middleware).
/// </summary>
internal interface ICurrentUserInitializer
{
    /// <summary>
    /// Set current user từ ClaimsPrincipal (từ JWT token).
    /// </summary>
    void SetCurrentUser(ClaimsPrincipal user);

    /// <summary>
    /// Set current user ID manually (cho background jobs/system operations).
    /// </summary>
    void SetCurrentUserId(string userId);
}
