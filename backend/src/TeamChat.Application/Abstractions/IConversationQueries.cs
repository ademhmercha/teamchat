using TeamChat.Application.Conversations.Dtos;

namespace TeamChat.Application.Abstractions;

/// <summary>
/// Acces aux donnees des conversations (cote requetes, sans logique metier).
/// La verification d'appartenance (membership) est faite par les services.
/// </summary>
public interface IConversationQueries
{
    Task<ConversationDto?> FindDirectConversationAsync(
        Guid userA,
        Guid userB,
        CancellationToken cancellationToken = default);

    Task<ConversationDto> CreateDirectAsync(
        Guid requestorId,
        Guid otherUserId,
        CancellationToken cancellationToken = default);

    Task<MessageDto> AddMessageAsync(
        Guid conversationId,
        Guid senderId,
        string content,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<MessageHistoryResult> GetMessagesAsync(
        Guid conversationId,
        long? before,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConversationListItemDto>> GetConversationsForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetMemberConversationIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}