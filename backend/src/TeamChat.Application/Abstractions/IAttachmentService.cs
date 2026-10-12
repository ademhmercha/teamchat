using TeamChat.Application.Attachments.Dtos;
using TeamChat.Application.Conversations.Dtos;

namespace TeamChat.Application.Abstractions;

/// <summary>
/// Enregistrement et lecture des pieces jointes (fichiers, images, messages vocaux).
/// Toutes les verifications de securite (appartenance, type reel, taille) sont
/// faites cote serveur.
/// </summary>
public interface IAttachmentService
{
    /// <summary>
    /// Valide puis stocke une piece jointe et cree le message associe dans la
    /// conversation. L'auteur doit etre membre de la conversation.
    /// </summary>
    Task<MessageDto> SaveAsync(
        Guid userId,
        Guid conversationId,
        AttachmentUpload upload,
        CancellationToken cancellationToken = default);

    /// <summary>Ouvre le contenu d'une piece jointe pour un membre de la conversation.</summary>
    Task<AttachmentDownload> OpenAsync(
        Guid userId,
        Guid attachmentId,
        CancellationToken cancellationToken = default);
}
