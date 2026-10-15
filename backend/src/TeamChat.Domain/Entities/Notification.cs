namespace TeamChat.Domain.Entities;

/// <summary>
/// Notification persistee pour un utilisateur (cloche de l'application).
/// Le titre et le corps sont figes au moment de l'emission pour rester
/// lisibles meme si l'acteur ou la conversation evoluent ensuite.
/// </summary>
public sealed class Notification : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>Destinataire de la notification.</summary>
    public Guid UserId { get; set; }

    public NotificationType Type { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    /// <summary>Conversation concernee (navigation au clic), si applicable.</summary>
    public Guid? ConversationId { get; set; }

    /// <summary>Utilisateur a l'origine de l'evenement (auteur, appelant).</summary>
    public Guid? ActorUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ReadAt { get; set; }
}
