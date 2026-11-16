using TeamChat.Application.Calls.Dtos;
using TeamChat.Domain.Entities;

namespace TeamChat.Application.Abstractions;

public interface ICallService
{
    Task<CallSessionDto> StartCallAsync(
        Guid callerId,
        Guid calleeId,
        CallType type,
        CancellationToken cancellationToken = default);

    Task<CallSessionDto> AcceptCallAsync(
        Guid userId,
        Guid callId,
        CancellationToken cancellationToken = default);

    Task<CallOutcomeDto> RejectCallAsync(
        Guid userId,
        Guid callId,
        CancellationToken cancellationToken = default);

    Task<CallOutcomeDto> EndCallAsync(
        Guid userId,
        Guid callId,
        CancellationToken cancellationToken = default);

    Task<CallOutcomeDto?> EndCallForDisconnectedUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<CallRelayDto> AuthorizeRelayAsync(
        Guid userId,
        Guid callId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CallExpiredEvent>> ExpireStaleCallsAsync(
        TimeSpan ringingTimeout,
        TimeSpan? maxDuration = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IceServerDto>> GetIceServersAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CallHistoryItemDto>> GetHistoryAsync(
        Guid userId,
        int limit = 50,
        CancellationToken cancellationToken = default);
}
