namespace TeamChat.Domain.Entities;

public sealed class Conversation : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public ConversationType Type { get; set; }

    public string? Name { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? DirectKey { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }
}