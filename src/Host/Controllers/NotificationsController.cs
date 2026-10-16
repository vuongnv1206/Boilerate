using Microsoft.AspNetCore.Authorization;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Models;
using Boilerate.Application.Notifications;
using Boilerate.Domain.Notifications;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Boilerate.Host.Controllers;

/// <summary>
/// Notifications API endpoints
/// </summary>
[Authorize]
[Route("api/notifications")]
public class NotificationsController : BaseApiController
{
    private readonly INotificationService _notificationService;
    private readonly ICurrentUser _currentUser;

    public NotificationsController(
        INotificationService notificationService,
        ICurrentUser currentUser)
    {
        _notificationService = notificationService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Get current user's notifications
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginationResponse<NotificationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyNotifications(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? isRead = null)
    {
        var userId = _currentUser.GetUserId();
        var result = await _notificationService.GetUserNotificationsAsync(
            userId, pageNumber, pageSize, isRead);

        return Ok(result);
    }

    /// <summary>
    /// Get unread notification count
    /// </summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = _currentUser.GetUserId();
        var count = await _notificationService.GetUnreadCountAsync(userId);

        return Ok(count);
    }

    /// <summary>
    /// Mark notification as read
    /// </summary>
    [HttpPut("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        await _notificationService.MarkAsReadAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Mark all notifications as read
    /// </summary>
    [HttpPut("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = _currentUser.GetUserId();
        await _notificationService.MarkAllAsReadAsync(userId);
        return NoContent();
    }

    /// <summary>
    /// Delete notification
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _notificationService.DeleteAsync(id);
        return NoContent();
    }

}
