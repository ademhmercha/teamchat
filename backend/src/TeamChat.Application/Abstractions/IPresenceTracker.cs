namespace TeamChat.Application.Abstractions;

public sealed record PresenceChange(Guid UserId, bool IsNowOnline);

public interface IPresenceTracker
{
    Task<PresenceChange> ConnectAsync(
        Guid userId,
        string connectionId,
        CancellationToken cancellationToken = default);

    Task<PresenceChange?> DisconnectAsync(
        string connectionId,
        CancellationToken cancellationToken = default);

    IReadOnlyList<Guid> GetOnlineUserIds();

    IReadOnlyList<string> GetConnectionIds(Guid userId);
}