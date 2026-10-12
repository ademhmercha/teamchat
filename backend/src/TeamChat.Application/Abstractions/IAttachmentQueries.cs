using TeamChat.Application.Conversations.Dtos;
using TeamChat.Domain.Entities;

namespace TeamChat.Application.Abstractions;

/// <summary>Acces aux donnees des pieces jointes (cote requetes, sans logique metier).</summary>
public interface IAttachmentQueries
{
    /// <summary>
    /// Cree l'<c>Attachment</c> et le <c>Message</c> associe (dans une seule transaction)
    /// puis renvoie la representation du message.
    /// </summary>
    Task<MessageDto> PersistAttachmentMessageAsync(
        Guid conversationId,
        Guid senderId,
        string storageKey,
        string originalFileName,
        string contentType,
        long sizeBytes,
        int? durationSec,
        MessageType type,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrouve une piece jointe pour le telechargement (avec la conversation pour
    /// verifier l'appartenance). Null si le message a ete supprime ou introuvable.
    /// </summary>
    Task<AttachmentDownloadInfo?> FindForDownloadAsync(
        Guid attachmentId,
        CancellationToken cancellationToken = default);
}

public sealed record AttachmentDownloadInfo(
    Guid AttachmentId,
    Guid ConversationId,
    string StorageKey,
    string FileName,
    string ContentType,
    long SizeBytes);
