namespace Boilerate.Domain.Notifications;

/// <summary>
/// Notification type for UI styling
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// Informational notification (blue)
    /// </summary>
    Info = 1,

    /// <summary>
    /// Success notification (green)
    /// </summary>
    Success = 2,

    /// <summary>
    /// Warning notification (yellow/orange)
    /// </summary>
    Warning = 3,

    /// <summary>
    /// Error notification (red)
    /// </summary>
    Error = 4
}
