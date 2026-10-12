namespace TeamChat.Domain.Entities;

public enum CallStatus
{
    Ringing = 0,
    Accepted = 1,
    Rejected = 2,
    Missed = 3,
    Ended = 4,
    Failed = 5,
}
