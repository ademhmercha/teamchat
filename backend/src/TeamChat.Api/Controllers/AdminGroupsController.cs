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
[Authorize(Roles = Roles.Admin)]
[Route("api/admin/groups")]
public sealed class AdminGroupsController : ControllerBase
{
    private readonly IAdminGroupService _groups;
    private readonly IHubContext<ChatHub, IChatClient> _hub;
    private readonly IPresenceTracker _presence;

    public AdminGroupsController(
        IAdminGroupService groups,
        IHubContext<ChatHub, IChatClient> hub,
        IPresenceTracker presence)
    {
        _groups = groups;
        _hub = hub;
        _presence = presence;
    }

    [HttpGet]
    public Task<IReadOnlyList<GroupDto>> GetGroups(CancellationToken cancellationToken)
        => _groups.GetGroupsAsync(cancellationToken);

    [HttpPost]
    [ProducesResponseType(typeof(GroupDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GroupDto>> Create(
        [FromBody] CreateGroupRequest request,
        CancellationToken cancellationToken)
    {
        var group = await _groups.CreateAsync(User.GetUserId(), request, cancellationToken);

        var members = group.Members.Select(m => m.UserId).ToList();
        await SyncMembershipAsync(new GroupOperationResult(group.Id, members, Array.Empty<Guid>(), members));

        return Created($"/api/admin/groups/{group.Id}", group);
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
        foreach (var userId in result.AddedUserIds)
        {
            foreach (var connectionId in _presence.GetConnectionIds(userId))
            {
                await _hub.Groups.AddToGroupAsync(connectionId, ChatGroups.Conversation(result.ConversationId));
            }

            await _hub.Clients.Group(ChatGroups.User(userId)).AddedToGroup(result.ConversationId);
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
