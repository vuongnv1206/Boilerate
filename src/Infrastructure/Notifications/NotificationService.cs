using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Models;
using Boilerate.Application.Notifications;
using Boilerate.Domain.Notifications;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
namespace Boilerate.Infrastructure.Notifications;
public sealed class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _actor;
    public NotificationService(ApplicationDbContext db, ICurrentUser actor) => (_db, _actor) = (db, actor);
    private void Own(Guid userId)
    {
        if (!_actor.IsAuthenticated() || userId == Guid.Empty || userId != _actor.GetUserId())
            throw new ForbiddenException("Notification access is limited to the current recipient.");
    }
    private IQueryable<NotificationReceipt> Visible(Guid userId) => _db.NotificationReceipts
        .Where(r => r.UserId == userId && r.HiddenOn == null && r.Notification.DeletedOn == null);
    private async Task<Guid> Store(Notification notification, IEnumerable<string> recipients, CancellationToken ct)
    {
        if (notification.Title.Length > 200 || notification.Message.Length > 1000)
            throw new FluentValidation.ValidationException("Notification title/message exceeds its limit.");
        _db.Notifications.Add(notification);
        foreach (var id in recipients.Distinct(StringComparer.Ordinal))
            _db.NotificationReceipts.Add(new NotificationReceipt { NotificationId = notification.Id, UserId = Guid.Parse(id) });
        // Payload and recipient snapshots are committed together; delivery retries happen after commit.
        await _db.SaveChangesAsync(ct);
        return notification.Id;
    }
    public async Task<Guid> SendToUserAsync(Guid userId, string title, string message, NotificationType type,
        string? referenceType = null, Guid? referenceId = null, string? actionUrl = null, CancellationToken cancellationToken = default)
    {
        var id = userId.ToString();
        if (!await _db.Users.AnyAsync(u => u.Id == id && u.IsActive, cancellationToken)) throw new NotFoundException("Recipient not found.");
        return await Store(Notification.CreateForUser(userId, title, message, type, referenceType, referenceId, actionUrl), new[] { id }, cancellationToken);
    }
    public async Task<Guid> SendToRoleAsync(string role, string title, string message, NotificationType type,
        string? referenceType = null, Guid? referenceId = null, string? actionUrl = null, CancellationToken cancellationToken = default)
    {
        var normalized = role.ToUpperInvariant();
        var ids = await (from membership in _db.UserRoles join r in _db.Roles on membership.RoleId equals r.Id
                        join u in _db.Users on membership.UserId equals u.Id
                        where r.NormalizedName == normalized && u.IsActive select u.Id).Distinct().ToListAsync(cancellationToken);
        return await Store(Notification.CreateForRole(role, title, message, type, referenceType, referenceId, actionUrl), ids, cancellationToken);
    }
    public async Task<Guid> SendBroadcastAsync(string title, string message, NotificationType type, string? actionUrl = null, CancellationToken cancellationToken = default) =>
        await Store(Notification.CreateBroadcast(title, message, type, actionUrl),
            await _db.Users.Where(u => u.IsActive).Select(u => u.Id).ToListAsync(cancellationToken), cancellationToken);
    public async Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var id = _actor.GetUserId(); Own(id);
        var receipt = await Visible(id).SingleOrDefaultAsync(r => r.NotificationId == notificationId, cancellationToken)
            ?? throw new NotFoundException("Notification not found.");
        receipt.IsRead = true; receipt.ReadOn ??= DateTime.UtcNow; await _db.SaveChangesAsync(cancellationToken);
    }
    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        Own(userId);
        await Visible(userId).Where(r => !r.IsRead).ExecuteUpdateAsync(s => s.SetProperty(r => r.ReadOn, DateTime.UtcNow).SetProperty(r => r.IsRead, true), cancellationToken);
    }
    public async Task DeleteAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var id = _actor.GetUserId(); Own(id);
        var receipt = await Visible(id).SingleOrDefaultAsync(r => r.NotificationId == notificationId, cancellationToken)
            ?? throw new NotFoundException("Notification not found.");
        receipt.HiddenOn = DateTime.UtcNow; await _db.SaveChangesAsync(cancellationToken);
    }
    public async Task<PaginationResponse<NotificationDto>> GetUserNotificationsAsync(Guid userId, int pageNumber, int pageSize,
        bool? isRead = null, CancellationToken cancellationToken = default)
    {
        Own(userId);
        if (pageNumber < 1 || pageSize < 1 || pageSize > 100 || ((long)pageNumber - 1) * pageSize > int.MaxValue)
            throw new FluentValidation.ValidationException("Invalid page; page size must be 1-100.");
        var query = Visible(userId).AsNoTracking();
        if (isRead.HasValue) query = query.Where(r => r.IsRead == isRead.Value);
        var count = await query.CountAsync(cancellationToken);
        var list = await query.OrderByDescending(r => r.Notification.CreatedOn).ThenBy(r => r.NotificationId)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).Select(r => new NotificationDto
            {
                Id = r.NotificationId, UserId = r.UserId, TargetRole = r.Notification.TargetRole,
                Title = r.Notification.Title, Message = r.Notification.Message, Type = r.Notification.Type,
                ReferenceType = r.Notification.ReferenceType, ReferenceId = r.Notification.ReferenceId,
                ActionUrl = r.Notification.ActionUrl, IsRead = r.IsRead, ReadOn = r.ReadOn, CreatedOn = r.Notification.CreatedOn
            }).ToListAsync(cancellationToken);
        return new PaginationResponse<NotificationDto>(list, count, pageNumber, pageSize);
    }
    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
    { Own(userId); return Visible(userId).CountAsync(r => !r.IsRead, cancellationToken); }
}
