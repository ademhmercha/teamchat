using TeamChat.Application.Conversations.Dtos;
using TeamChat.Domain.Entities;

namespace TeamChat.Application.Calls.Dtos;

/// <summary>Demande d'ouverture d'un appel (methode hub CallUser).</summary>
public sealed record StartCallRequest(Guid CalleeId, CallType Type);

/// <summary>Session d'appel telle que renvoyee a l'appelant.</summary>
public sealed record CallSessionDto(
    Guid CallId,
    Guid CallerId,
    string CallerDisplayName,
    Guid CalleeId,
    string CalleeDisplayName,
    CallType Type,
    CallStatus Status,
    Guid ConversationId,
    DateTimeOffset StartedAt);

/// <summary>Resultat d'une transition d'appel : qui notifier et quel message systeme eventuel.</summary>
public sealed record CallOutcomeDto(
    Guid CallId,
    Guid CallerId,
    Guid CalleeId,
    CallType Type,
    CallStatus Status,
    MessageDto? SystemMessage);

/// <summary>Autorisation de relayer de la signalisation WebRTC vers le pair.</summary>
public sealed record CallRelayDto(
    Guid CallId,
    Guid PeerUserId,
    CallType Type);

/// <summary>Appel expire (sans reponse) a notifier aux deux participants.</summary>
public sealed record CallExpiredEvent(
    Guid CallId,
    Guid CallerId,
    Guid CalleeId,
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
