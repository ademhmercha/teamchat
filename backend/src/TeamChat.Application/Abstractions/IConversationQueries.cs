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

    /// <summary>Ajoute un message systeme (ex. fin d'appel) dans une conversation.</summary>
    Task<MessageDto> AddSystemMessageAsync(
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

    /// <summary>Charge un message par identifiant (toutes conversations confondues).</summary>
    Task<MessageDto?> FindMessageAsync(
        Guid messageId,
        CancellationToken cancellationToken = default);

    /// <summary>Met a jour le contenu d'un message et renvoie sa representation a jour.</summary>
    Task<MessageDto> UpdateMessageAsync(
        Guid messageId,
        string content,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>Marque un message comme supprime (suppression douce) et le renvoie.</summary>
    Task<MessageDto> SoftDeleteMessageAsync(
        Guid messageId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>Identifiant du dernier message d'une conversation (null si aucun message).</summary>
    Task<Guid?> GetLastMessageIdAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    /// <summary>Positionne le dernier message lu du participant (sans jamais reculer).</summary>
    Task SetLastReadAsync(
        Guid conversationId,
        Guid userId,
        Guid? messageId,
        CancellationToken cancellationToken = default);
}