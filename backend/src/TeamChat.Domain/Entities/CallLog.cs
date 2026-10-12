namespace TeamChat.Domain.Entities;

/// <summary>
/// Journal d'un appel 1-a-1 (audio ou video). Le cycle de vie est pilote par le
/// hub de signalisation : Ringing -> Accepted -> Ended, ou Rejected / Missed / Failed.
/// Les participants sont toujours deux utilisateurs (pas d'appel de groupe).
/// </summary>
public sealed class CallLog
{
    public Guid Id { get; set; }

    public Guid CallerId { get; set; }

    public Guid CalleeId { get; set; }

    public CallType Type { get; set; }

    public CallStatus Status { get; set; } = CallStatus.Ringing;

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? AnsweredAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }
}
