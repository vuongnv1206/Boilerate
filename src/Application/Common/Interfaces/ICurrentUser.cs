using System.Security.Claims;

namespace Boilerate.Application.Common.Interfaces;

/// <summary>
/// Interface để lấy thông tin user hiện tại từ JWT token.
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// User name từ Identity.Name.
    /// </summary>
    string? Name { get; }

    /// <summary>
    /// Lấy User ID (Guid) từ NameIdentifier claim.
    /// </summary>
    Guid GetUserId();

    /// <summary>
    /// Lấy User Email từ Email claim.
    /// </summary>
    string? GetUserEmail();

    /// <summary>
    /// Check user đã authenticate chưa.
    /// </summary>
    bool IsAuthenticated();

    /// <summary>
    /// Check user có role cụ thể không.
    /// </summary>
    bool IsInRole(string role);

    /// <summary>
    /// Lấy tất cả claims của user.
    /// </summary>
    IEnumerable<Claim>? GetUserClaims();
    
    /// <summary>
    /// Lấy danh sách các roles của user.
    /// </summary>
    List<string>? GetRoles();
}
