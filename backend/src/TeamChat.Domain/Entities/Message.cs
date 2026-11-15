namespace TeamChat.Domain.Entities;

public sealed class Message : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

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