using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TeamChat.Api.Hubs;
using TeamChat.Api.Infrastructure;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Conversations.Dtos;
using TeamChat.Domain.Constants;

namespace TeamChat.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.CompanyAdmins)]
[Route("api/admin/groups")]
public sealed class AdminGroupsController : ControllerBase
{
    private readonly IAdminGroupService _groups;
    private readonly INotificationService _notifications;
    private readonly IHubContext<ChatHub, IChatClient> _hub;
    private readonly IPresenceTracker _presence;

    public AdminGroupsController(
        IAdminGroupService groups,
        INotificationService notifications,
        IHubContext<ChatHub, IChatClient> hub,
        IPresenceTracker presence)
    {
        _groups = groups;
        _notifications = notifications;
        _hub = hub;
        _presence = presence;
    }

    [HttpGet]
    public Task<IReadOnlyList<GroupDto>> GetGroups(CancellationToken cancellationToken)
        => _groups.GetGroupsAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GroupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<GroupDto> GetGroup(Guid id, CancellationToken cancellationToken)
        => _groups.GetGroupAsync(id, cancellationToken);

    [HttpPost]
    [ProducesResponseType(typeof(GroupDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GroupDto>> Create(
        [FromBody] CreateGroupRequest request,
        CancellationToken cancellationToken)
    {
        var group = await _groups.CreateAsync(User.GetUserId(), request, cancellationToken);

        var members = group.Members.Select(m => m.UserId).ToList();
        await SyncMembershipAsync(new GroupOperationResult(group.Id, group.Name, members, Array.Empty<Guid>(), members));

        return Created($"/api/admin/groups/{group.Id}", group);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(GroupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GroupDto>> Rename(
        Guid id,
        [FromBody] RenameGroupRequest request,
        CancellationToken cancellationToken)
    {
        var group = await _groups.RenameAsync(id, request, cancellationToken);
        await NotifyConversationUpdatedAsync(group.Members.Select(m => m.UserId).ToList(), group.Id);
        return group;
    }

    [HttpPost("{id:guid}/unarchive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unarchive(Guid id, CancellationToken cancellationToken)
    {
        var result = await _groups.UnarchiveAsync(id, cancellationToken);
        await NotifyConversationUpdatedAsync(result.CurrentMemberIds, result.ConversationId);
        return NoContent();
    }

    [HttpDelete("{id:guid}/permanent")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePermanently(Guid id, CancellationToken cancellationToken)
    {
        var result = await _groups.DeleteAsync(id, cancellationToken);

        foreach (var userId in result.CurrentMemberIds)
        {
            foreach (var connectionId in _presence.GetConnectionIds(userId))
            {
                await _hub.Groups.RemoveFromGroupAsync(connectionId, ChatGroups.Conversation(id));
            }
        }

        await NotifyConversationUpdatedAsync(result.CurrentMemberIds, id);
        return NoContent();
    }

    [HttpPut("{id:guid}/members")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMembers(
        Guid id,
        [FromBody] UpdateGroupMembersRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _groups.UpdateMembersAsync(id, request, cancellationToken);
        await SyncMembershipAsync(result);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var result = await _groups.ArchiveAsync(id, cancellationToken);

        foreach (var userId in result.CurrentMemberIds)
        {
            foreach (var connectionId in _presence.GetConnectionIds(userId))
            {
                await _hub.Groups.RemoveFromGroupAsync(connectionId, ChatGroups.Conversation(id));
            }
        }

        await NotifyConversationUpdatedAsync(result.CurrentMemberIds, id);
        return NoContent();
    }

    private async Task SyncMembershipAsync(GroupOperationResult result)
    {
        var actorId = User.GetUserId();

        foreach (var userId in result.AddedUserIds)
        {
            foreach (var connectionId in _presence.GetConnectionIds(userId))
            {
                await _hub.Groups.AddToGroupAsync(connectionId, ChatGroups.Conversation(result.ConversationId));
            }

            await _hub.Clients.Group(ChatGroups.User(userId)).AddedToGroup(result.ConversationId);

            if (userId == actorId)
            {
                continue;
            }

            try
            {
                await _notifications.NotifyGroupAddedAsync(
                    userId, result.ConversationId, result.GroupName, actorId);
                await _hub.Clients.Group(ChatGroups.User(userId)).NotificationsUpdated();
            }
            catch
            {
                // Une notification ne doit jamais faire echouer la mutation de groupe.
            }
        }

        foreach (var userId in result.RemovedUserIds)
        {
            foreach (var connectionId in _presence.GetConnectionIds(userId))
            {
                await _hub.Groups.RemoveFromGroupAsync(connectionId, ChatGroups.Conversation(result.ConversationId));
            }
        }

        await NotifyConversationUpdatedAsync(result.CurrentMemberIds, result.ConversationId);
    }

    private async Task NotifyConversationUpdatedAsync(IReadOnlyList<Guid> userIds, Guid conversationId)
    {
        foreach (var userId in userIds.Distinct())
        {
            await _hub.Clients.Group(ChatGroups.User(userId)).ConversationUpdated(conversationId);
        }
    }
}
