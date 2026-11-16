namespace TeamChat.Domain.Entities;

public sealed class CallLog : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CallerId { get; set; }

    public Guid CalleeId { get; set; }

    public CallType Type { get; set; }

    public CallStatus Status { get; set; } = CallStatus.Ringing;

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? AnsweredAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }
}
