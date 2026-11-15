using TeamChat.Application.Conversations.Dtos;
using TeamChat.Domain.Entities;

namespace TeamChat.Application.Calls.Dtos;

public sealed record CallSessionDto(
    Guid CallId,
    Guid CallerId,
    string CallerDisplayName,
    Guid CalleeId,
    string CalleeDisplayName,
    string? CallerAvatarUrl,
    string? CalleeAvatarUrl,
    CallType Type,
    CallStatus Status,
    Guid ConversationId,
    DateTimeOffset StartedAt);

public sealed record CallOutcomeDto(
    Guid CallId,
    Guid CallerId,
    Guid CalleeId,
    CallType Type,
    CallStatus Status,
    MessageDto? SystemMessage);

public sealed record CallRelayDto(
    Guid CallId,
    Guid PeerUserId,
    CallType Type);

public sealed record CallExpiredEvent(
    Guid CallId,
    Guid CallerId,
    Guid CalleeId,
    CallType Type,
    CallStatus Status,
    MessageDto? SystemMessage);

public sealed record IceServerDto(
    IReadOnlyList<string> Urls,
    string? Username,
    string? Credential);

public sealed record CallHistoryItemDto(
    Guid Id,
    Guid OtherUserId,
    string OtherDisplayName,
    CallType Type,
    CallStatus Status,
    DateTimeOffset StartedAt,
    int? DurationSeconds,
    bool Outgoing);
