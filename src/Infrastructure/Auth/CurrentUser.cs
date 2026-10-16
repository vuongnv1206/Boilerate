using Boilerate.Application.Common.Interfaces;
using Boilerate.Shared.Authorization;
using System.Security.Claims;

namespace Boilerate.Infrastructure.Auth;

/// <summary>
/// Implementation của ICurrentUser và ICurrentUserInitializer.
/// Scoped per request - mỗi HTTP request có instance riêng.
/// </summary>
public class CurrentUser : ICurrentUser, ICurrentUserInitializer
{
    private ClaimsPrincipal? _user;
    private Guid _userId = Guid.Empty;
    private bool _initialized;

    /// <summary>
    /// User name từ Identity.Name.
    /// </summary>
    public string? Name => _user?.Identity?.Name;

    /// <summary>
    /// Lấy User ID từ NameIdentifier claim.
    /// </summary>
    public Guid GetUserId() => _userId;

    /// <summary>
    /// Lấy User Email từ Email claim.
    /// </summary>
    public string? GetUserEmail() =>
        IsAuthenticated()
            ? _user!.GetEmail()
            : string.Empty;

    /// <summary>
    /// Check user đã authenticate chưa.
    /// </summary>
    public bool IsAuthenticated() =>
        _user?.Identity?.IsAuthenticated is true;

    /// <summary>
    /// Check user có role không.
    /// </summary>
    public bool IsInRole(string role) =>
        _user?.IsInRole(role) is true;

    /// <summary>
    /// Lấy tất cả claims.
    /// </summary>
    public IEnumerable<Claim>? GetUserClaims() =>
        _user?.Claims;

    /// <summary>
    /// Lấy danh sách các roles.
    /// </summary>
    public List<string>? GetRoles() =>
        _user?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

    /// <summary>
    /// Set current user từ ClaimsPrincipal.
    /// Chỉ được gọi một lần per request (từ middleware).
    /// </summary>
    public void SetCurrentUser(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (_initialized)
        {
            throw new InvalidOperationException("Method reserved for in-scope initialization");
        }

        if (user.Identity?.IsAuthenticated == true)
        {
            if (!Guid.TryParse(user.GetUserId(), out var userId) || userId == Guid.Empty)
            {
                throw new Boilerate.Application.Common.Exceptions.UnauthorizedException("Invalid user identity.");
            }

            _userId = userId;
        }

        _user = user;
        _initialized = true;
    }

    /// <summary>
    /// Set current user ID manually (cho background jobs).
    /// </summary>
    public void SetCurrentUserId(string userId)
    {
        if (_initialized)
        {
            throw new InvalidOperationException("Method reserved for in-scope initialization");
        }

        if (!Guid.TryParse(userId, out var actorId) || actorId == Guid.Empty)
        {
            throw new ArgumentException("A non-empty user ID is required.", nameof(userId));
        }

        _userId = actorId;
        _initialized = true;
    }
}
