using Boilerate.Domain.Notifications;

namespace Boilerate.Application.Notifications;

/// <summary>
/// Notification DTO for API responses
/// </summary>
public class NotificationDto
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? TargetRole { get; set; }
    public string Title { get; set; } = default!;
    public string Message { get; set; } = default!;
    public NotificationType Type { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? ActionUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadOn { get; set; }
    public DateTime CreatedOn { get; set; }
}

/// <summary>
/// Simplified notification DTO for real-time push
/// </summary>
public class NotificationPushDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string Message { get; set; } = default!;
    public NotificationType Type { get; set; }
    public string? ActionUrl { get; set; }
    public DateTime CreatedOn { get; set; }
}
