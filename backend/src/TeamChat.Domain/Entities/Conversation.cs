namespace TeamChat.Domain.Entities;

/// <summary>
/// Conversation privee (directe) ou de groupe.
/// Les conversations directes portent un DirectKey unique ("d:{idMin}:{idMax}")
/// pour garantir l'unicite (obtenir ou creer de maniere idempotente).
/// </summary>
public sealed class Conversation
{
    public Guid Id { get; set; }

    public ConversationType Type { get; set; }

    /// <summary>Nom de la conversation (nul pour les conversations directes).</summary>
    public string? Name { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? DirectKey { get; set; }

    /// <summary>Date d'archivage (les groupes archives sont masques et inaccessibles).</summary>
    public DateTimeOffset? ArchivedAt { get; set; }
}