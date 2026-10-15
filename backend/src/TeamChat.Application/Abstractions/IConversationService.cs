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

    /// <summary>Modifie un message texte. Reserve a l'auteur, message non supprime.</summary>
    Task<MessageDto> EditMessageAsync(
        Guid userId,
        Guid messageId,
        string newContent,
        CancellationToken cancellationToken = default);

    /// <summary>Supprime (logiquement) un message. Reserve a l'auteur.</summary>
    Task<MessageDto> DeleteMessageAsync(
        Guid userId,
        Guid messageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marque la conversation comme lue jusqu'au message indique
    /// (ou jusqu'au dernier message si <paramref name="messageId"/> est null).
    /// </summary>
    Task MarkAsReadAsync(
        Guid userId,
        Guid conversationId,
        Guid? messageId,
        CancellationToken cancellationToken = default);

    /// <summary>Detail d'une conversation (participants, nom, archivage) pour un membre.</summary>
    Task<ConversationDetailDto> GetDetailAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken = default);

    /// <summary>Liste des participants d'une conversation (membre uniquement).</summary>
    Task<IReadOnlyList<ConversationParticipantDto>> GetMembersAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken = default);

    /// <summary>Quitte un groupe. Interdit pour une conversation directe (utiliser l'archivage).</summary>
    Task<LeaveConversationResult> LeaveAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken = default);

    /// <summary>Archive ou desarchive une conversation pour l'utilisateur courant.</summary>
    Task SetArchivedAsync(
        Guid userId,
        Guid conversationId,
        bool archived,
        CancellationToken cancellationToken = default);

    /// <summary>Fichiers partages dans une conversation (membre uniquement).</summary>
    Task<IReadOnlyList<SharedAttachmentDto>> GetSharedAttachmentsAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken = default);

    /// <summary>Recherche dans les messages d'une conversation (membre uniquement).</summary>
    Task<MessageHistoryResult> SearchMessagesAsync(
        Guid userId,
        Guid conversationId,
        string query,
        int limit,
        CancellationToken cancellationToken = default);
}