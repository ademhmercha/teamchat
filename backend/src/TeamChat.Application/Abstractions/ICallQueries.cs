using TeamChat.Domain.Entities;

namespace TeamChat.Application.Abstractions;

public sealed record CallRecord(
    Guid Id,
    Guid CallerId,
    Guid CalleeId,
    CallType Type,
    CallStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? AnsweredAt,
    DateTimeOffset? EndedAt);

public interface ICallQueries
{
    Task<Guid> CreateAsync(
        Guid callerId,
        Guid calleeId,
        CallType type,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<CallRecord?> FindAsync(Guid callId, CancellationToken cancellationToken = default);

    Task<CallRecord?> FindActiveCallForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task UpdateStatusAsync(
        Guid callId,
        CallStatus status,
        DateTimeOffset? answeredAt,
        DateTimeOffset? endedAt,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CallRecord>> FindStaleRingingAsync(
        DateTimeOffset startedBefore,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CallRecord>> FindStaleAcceptedAsync(
        DateTimeOffset startedBefore,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CallRecord>> GetHistoryForUserAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default);
}
