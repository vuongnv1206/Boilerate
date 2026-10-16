using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Models;
using Boilerate.Domain.Notifications;

namespace Boilerate.Application.Notifications;

/// <summary>
/// Notification service interface
/// </summary>
public interface INotificationService : ITransientService
{
    /// <summary>
    /// Send notification to specific user
    /// </summary>
    Task<Guid> SendToUserAsync(
        Guid userId,
        string title,
        string message,
        NotificationType type,
        string? referenceType = null,
        Guid? referenceId = null,
        string? actionUrl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send notification to all users with specific role
    /// </summary>
    Task<Guid> SendToRoleAsync(
        string role,
        string title,
        string message,
        NotificationType type,
        string? referenceType = null,
        Guid? referenceId = null,
        string? actionUrl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send broadcast notification to all users
    /// </summary>
    Task<Guid> SendBroadcastAsync(
        string title,
        string message,
        NotificationType type,
        string? actionUrl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark notification as read
    /// </summary>
    Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark all user notifications as read
    /// </summary>
    Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete notification
    /// </summary>
    Task DeleteAsync(Guid notificationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get user notifications with pagination
    /// </summary>
    Task<PaginationResponse<NotificationDto>> GetUserNotificationsAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        bool? isRead = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get unread notification count for user
    /// </summary>
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);
}
