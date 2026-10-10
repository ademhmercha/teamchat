using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public ConversationsController(IConversationService conversations)
    {
        _conversations = conversations;
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

    [HttpGet("{conversationId:guid}/messages")]
    [ProducesResponseType(typeof(MessageHistoryResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<MessageHistoryResult> GetMessages(
        Guid conversationId,
        [FromQuery] long? before,
        [FromQuery, System.ComponentModel.DataAnnotations.RangeAttribute(1, 100)] int limit = 50,
        CancellationToken cancellationToken = default)
        => _conversations.GetHistoryAsync(User.GetUserId(), conversationId, before, limit, cancellationToken);
}