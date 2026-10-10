namespace TeamChat.Domain.Entities;

public sealed class ConversationParticipant
{
    public Guid ConversationId { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? LastReadMessageId { get; set; }
}