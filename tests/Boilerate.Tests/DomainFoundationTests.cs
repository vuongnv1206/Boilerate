using Boilerate.Domain.Common.Contracts;
using Boilerate.Domain.Identity;
using Boilerate.Domain.Notifications;
using Xunit;

namespace Boilerate.Tests;

public sealed class DomainFoundationTests
{
    [Fact]
    public void AuditableEntitiesHaveDistinctIdsBeforePersistence()
    {
        AuditableEntity[] entities =
        [
            new Permission("role", Guid.NewGuid(), Guid.NewGuid()),
            new Permission("role", Guid.NewGuid(), Guid.NewGuid()),
            Notification.CreateForUser(Guid.NewGuid(), "Title", "Message", NotificationType.Info),
            Notification.CreateForRole("role", "Title", "Message", NotificationType.Info),
            Notification.CreateBroadcast("Title", "Message", NotificationType.Info)
        ];

        Assert.All(entities, entity => Assert.NotEqual(Guid.Empty, entity.Id));
        Assert.Equal(entities.Length, entities.Select(entity => entity.Id).Distinct().Count());
    }

    [Fact]
    public void NotificationReceiptCanReferenceItsNotificationBeforePersistence()
    {
        Guid recipientId = Guid.NewGuid();
        Notification notification = Notification.CreateForUser(recipientId, "Title", "Message", NotificationType.Info);
        var receipt = new NotificationReceipt
        {
            NotificationId = notification.Id,
            Notification = notification,
            UserId = recipientId
        };

        notification.MarkAsSent();

        Assert.NotEqual(Guid.Empty, receipt.NotificationId);
        Assert.Equal(notification.Id, receipt.NotificationId);
        Assert.Equal(recipientId, receipt.UserId);
        Assert.True(notification.IsSent);
        Assert.False(receipt.IsRead);
        Assert.Null(receipt.ReadOn);
    }
}
