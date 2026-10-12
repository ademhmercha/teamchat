using TeamChat.Domain.Entities;

namespace TeamChat.Application.Abstractions;

/// <summary>Vue persistee d'un appel (sans logique metier).</summary>
public sealed record CallRecord(
    Guid Id,
    Guid CallerId,
    Guid CalleeId,
    CallType Type,
    CallStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? AnsweredAt,
    DateTimeOffset? EndedAt);

/// <summary>Acces aux donnees des appels.</summary>
public interface ICallQueries
{
    Task<Guid> CreateAsync(
        Guid callerId,
        Guid calleeId,
        CallType type,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<CallRecord?> FindAsync(Guid callId, CancellationToken cancellationToken = default);

    /// <summary>Appel en cours (sonnerie ou accepte) impliquant l'utilisateur, s'il existe.</summary>
    Task<CallRecord?> FindActiveCallForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task UpdateStatusAsync(
        Guid callId,
        CallStatus status,
        DateTimeOffset? answeredAt,
        DateTimeOffset? endedAt,
        CancellationToken cancellationToken = default);

    /// <summary>Appels encore en sonnerie demarres avant l'instant indique.</summary>
    Task<IReadOnlyList<CallRecord>> FindStaleRingingAsync(
        DateTimeOffset startedBefore,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CallRecord>> GetHistoryForUserAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default);
}
