using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TeamChat.Api.Hubs;
using TeamChat.Api.Infrastructure;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Conversations.Dtos;

namespace TeamChat.Api.Controllers;

[ApiController]
[Route("api/conversations")]
[Authorize]
public sealed class ConversationsController : ControllerBase
{
    private readonly IConversationService _conversations;
    private readonly IHubContext<ChatHub, IChatClient> _hub;
    private readonly IPresenceTracker _presence;

    public ConversationsController(
        IConversationService conversations,
        IHubContext<ChatHub, IChatClient> hub,
        IPresenceTracker presence)
    {
        _conversations = conversations;
        _hub = hub;
        _presence = presence;
    }

    [HttpGet]
    public Task<IReadOnlyList<ConversationListItemDto>> GetMyConversations(CancellationToken cancellationToken)
        => _conversations.GetMyConversationsAsync(User.GetUserId(), cancellationToken);

    [HttpPost("direct")]
    [ProducesResponseType(typeof(ConversationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ConversationDto> CreateDirect(
        CreateDirectConversationRequest request,
        CancellationToken cancellationToken)
        => _conversations.GetOrCreateDirectAsync(User.GetUserId(), request.UserId, cancellationToken);

    [HttpGet("{conversationId:guid}")]
    [ProducesResponseType(typeof(ConversationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ConversationDetailDto> GetDetail(Guid conversationId, CancellationToken cancellationToken)
        => _conversations.GetDetailAsync(User.GetUserId(), conversationId, cancellationToken);

    [HttpGet("{conversationId:guid}/members")]
    [ProducesResponseType(typeof(IReadOnlyList<ConversationParticipantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IReadOnlyList<ConversationParticipantDto>> GetMembers(Guid conversationId, CancellationToken cancellationToken)
        => _conversations.GetMembersAsync(User.GetUserId(), conversationId, cancellationToken);

    [HttpPatch("{conversationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateConversation(
        Guid conversationId,
        [FromBody] UpdateConversationRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await _conversations.SetArchivedAsync(userId, conversationId, request.Archived, cancellationToken);

        await _hub.Clients.Group(ChatGroups.User(userId)).ConversationUpdated(conversationId);
        return NoContent();
    }

    [HttpPost("{conversationId:guid}/leave")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Leave(Guid conversationId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await _conversations.LeaveAsync(userId, conversationId, cancellationToken);

        foreach (var connectionId in _presence.GetConnectionIds(userId))
        {
            await _hub.Groups.RemoveFromGroupAsync(connectionId, ChatGroups.Conversation(conversationId));
        }

        await _hub.Clients.Group(ChatGroups.User(userId)).ConversationUpdated(conversationId);

        foreach (var memberId in result.RemainingMemberIds.Distinct())
        {
            await _hub.Clients.Group(ChatGroups.User(memberId)).ConversationUpdated(conversationId);
        }

        return NoContent();
    }

    [HttpGet("{conversationId:guid}/attachments")]
    [ProducesResponseType(typeof(IReadOnlyList<SharedAttachmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IReadOnlyList<SharedAttachmentDto>> GetSharedAttachments(Guid conversationId, CancellationToken cancellationToken)
        => _conversations.GetSharedAttachmentsAsync(User.GetUserId(), conversationId, cancellationToken);

    [HttpGet("{conversationId:guid}/messages/search")]
    [ProducesResponseType(typeof(MessageHistoryResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<MessageHistoryResult> SearchMessages(
        Guid conversationId,
        [FromQuery] string q,
        [FromQuery, System.ComponentModel.DataAnnotations.RangeAttribute(1, 50)] int limit = 30,
        CancellationToken cancellationToken = default)
        => _conversations.SearchMessagesAsync(User.GetUserId(), conversationId, q ?? string.Empty, limit, cancellationToken);

    [HttpGet("{conversationId:guid}/messages")]
    [ProducesResponseType(typeof(MessageHistoryResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<MessageHistoryResult> GetMessages(
        Guid conversationId,
        [FromQuery] long? before,
        [FromQuery, System.ComponentModel.DataAnnotations.RangeAttribute(1, 100)] int limit = 50,
        CancellationToken cancellationToken = default)
        => _conversations.GetHistoryAsync(User.GetUserId(), conversationId, before, limit, cancellationToken);

    [HttpPost("{conversationId:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MarkAsRead(
        Guid conversationId,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] MarkAsReadRequest? request,
        CancellationToken cancellationToken)
    {
        await _conversations.MarkAsReadAsync(User.GetUserId(), conversationId, request?.MessageId, cancellationToken);
        return NoContent();
    }
}