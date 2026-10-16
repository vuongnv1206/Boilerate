using Boilerate.Domain.Common.Contracts;

namespace Boilerate.Domain.Notifications;

/// <summary>
/// Notification entity - stores all notifications sent to users
/// </summary>
public sealed class Notification : AuditableEntity, IAggregateRoot
{
    /// <summary>
    /// Target user ID (null = broadcast to all users)
    /// </summary>
    public Guid? UserId { get; private set; }

    /// <summary>
    /// Target role (null = specific user, not null = all users with this role)
    /// </summary>
    public string? TargetRole { get; private set; }

    /// <summary>
    /// Notification title
    /// </summary>
    public string Title { get; private set; } = default!;

    /// <summary>
    /// Notification message/content
    /// </summary>
    public string Message { get; private set; } = default!;

    /// <summary>
    /// Notification type (Info, Success, Warning, Error)
    /// </summary>
    public NotificationType Type { get; private set; }

    /// <summary>
    /// Reference entity type (e.g., "Product", "Order")
    /// </summary>
    public string? ReferenceType { get; private set; }

    /// <summary>
    /// Reference entity ID
    /// </summary>
    public Guid? ReferenceId { get; private set; }

    /// <summary>
    /// Action URL (where to navigate when clicked)
    /// </summary>
    public string? ActionUrl { get; private set; }

    /// <summary>
    /// Is notification sent successfully
    /// </summary>
    public bool IsSent { get; private set; }

    /// <summary>
    /// When notification was sent
    /// </summary>
    public DateTime? SentOn { get; private set; }

    // EF Core constructor
    private Notification()
    {
    }

    // ==================== Factory Methods ====================

    /// <summary>
    /// Create notification for specific user
    /// </summary>
    public static Notification CreateForUser(
        Guid userId,
        string title,
        string message,
        NotificationType type,
        string? referenceType = null,
        Guid? referenceId = null,
        string? actionUrl = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required", nameof(title));

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message is required", nameof(message));

        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            ActionUrl = actionUrl,
            IsSent = false
        };

        return notification;
    }

    /// <summary>
    /// Create notification for role (broadcast to all users with this role)
    /// </summary>
    public static Notification CreateForRole(
        string role,
        string title,
        string message,
        NotificationType type,
        string? referenceType = null,
        Guid? referenceId = null,
        string? actionUrl = null)
    {
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role is required", nameof(role));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required", nameof(title));

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message is required", nameof(message));

        var notification = new Notification
        {
            TargetRole = role,
            Title = title,
            Message = message,
            Type = type,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            ActionUrl = actionUrl,
            IsSent = false
        };

        return notification;
    }

    /// <summary>
    /// Create broadcast notification (to all users)
    /// </summary>
    public static Notification CreateBroadcast(
        string title,
        string message,
        NotificationType type,
        string? actionUrl = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required", nameof(title));

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message is required", nameof(message));

        var notification = new Notification
        {
            Title = title,
            Message = message,
            Type = type,
            ActionUrl = actionUrl,
            IsSent = false
        };

        return notification;
    }

    // ==================== Business Logic Methods ====================

    /// <summary>
    /// Mark notification as sent
    /// </summary>
    public void MarkAsSent()
    {
        if (IsSent)
            return;

        IsSent = true;
        SentOn = DateTime.UtcNow;
    }

    /// <summary>
    /// Check if notification is for specific user
    /// </summary>
    public bool IsForUser(Guid userId) => UserId.HasValue && UserId.Value == userId;

    /// <summary>
    /// Check if notification is for role
    /// </summary>
    public bool IsForRole() => !string.IsNullOrWhiteSpace(TargetRole);

    /// <summary>
    /// Check if notification is broadcast
    /// </summary>
    public bool IsBroadcast() => !UserId.HasValue && string.IsNullOrWhiteSpace(TargetRole);
}
