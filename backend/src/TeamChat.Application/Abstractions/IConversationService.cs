using TeamChat.Application.Conversations.Dtos;

namespace TeamChat.Application.Abstractions;

public interface IConversationService
{
    /// <summary>
    /// Obtient la conversation directe existante entre les deux utilisateurs,
    /// ou la cree si elle n'existe pas encore (idempotent, unicite garantie).
    /// </summary>
    Task<ConversationDto> GetOrCreateDirectAsync(
        Guid userId,
        Guid otherUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Envoie un message texte dans une conversation dont l'auteur est membre.</summary>
    Task<MessageDto> SendMessageAsync(
        Guid userId,
        Guid conversationId,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>Historique pagine par curseur (Sequence, ordre chronologique inverse).</summary>
    Task<MessageHistoryResult> GetHistoryAsync(
        Guid userId,
        Guid conversationId,
        long? before,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Liste des conversations de l'utilisateur, avec dernier message.</summary>
    Task<IReadOnlyList<ConversationListItemDto>> GetMyConversationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>Identifiants des conversations dont l'utilisateur est membre (pour les groupes SignalR).</summary>
    Task<IReadOnlyList<Guid>> GetMemberConversationIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}