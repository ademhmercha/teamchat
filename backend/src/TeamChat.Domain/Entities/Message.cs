namespace TeamChat.Domain.Entities;

/// <summary>
/// Message d'une conversation. Le contenu concrete peut etre du texte pur
/// (Text) ou une reference a une piece jointe (AttachmentId) pour les
/// messages vocaux, fichiers et images.
/// </summary>
public sealed class Message : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>Ordre chronologique global (identity). Sert de curseur de pagination fiable.</summary>
    public long Sequence { get; set; }

    public Guid ConversationId { get; set; }

    public Guid SenderId { get; set; }

    public MessageType Type { get; set; } = MessageType.Text;

    public string? Content { get; set; }

    public Guid? AttachmentId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? EditedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}