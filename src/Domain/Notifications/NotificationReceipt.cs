namespace Boilerate.Domain.Notifications;

/// <summary>Delivery, read and hide state belonging to exactly one recipient.</summary>
public sealed class NotificationReceipt
{
    public Guid NotificationId { get; set; }
    public Guid UserId { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadOn { get; set; }
    public DateTime? HiddenOn { get; set; }
    public Notification Notification { get; set; } = default!;
}
