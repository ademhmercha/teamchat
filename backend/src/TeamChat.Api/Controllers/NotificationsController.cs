using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TeamChat.Api.Hubs;
using TeamChat.Api.Infrastructure;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Notifications.Dtos;

namespace TeamChat.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;
    private readonly IHubContext<ChatHub, IChatClient> _hub;

    public NotificationsController(INotificationService notifications, IHubContext<ChatHub, IChatClient> hub)
    {
        _notifications = notifications;
        _hub = hub;
    }

    [HttpGet]
    [ProducesResponseType(typeof(NotificationListDto), StatusCodes.Status200OK)]
    public Task<NotificationListDto> GetMine(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
        => _notifications.GetMineAsync(User.GetUserId(), unreadOnly, Math.Clamp(limit, 1, 100), cancellationToken);

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var updated = await _notifications.MarkReadAsync(userId, id, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        await _hub.Clients.Group(ChatGroups.User(userId)).NotificationsUpdated();
        return NoContent();
    }

    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await _notifications.MarkAllReadAsync(userId, cancellationToken);
        await _hub.Clients.Group(ChatGroups.User(userId)).NotificationsUpdated();
        return NoContent();
    }
}
