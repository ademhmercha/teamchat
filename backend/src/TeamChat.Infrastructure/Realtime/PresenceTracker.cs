using System.Collections.Concurrent;
using TeamChat.Application.Abstractions;

namespace TeamChat.Infrastructure.Realtime;

/// <summary>
/// Suivi de presence en memoire (par instance). Un utilisateur est en ligne
/// des qu'au moins une connexion SignalR est active pour lui. En mono-instance
/// pour ce sprint ; un backend deborde sera legerement different (section 7).
/// </summary>
public sealed class PresenceTracker : IPresenceTracker
{
    private readonly ConcurrentDictionary<string, Guid> _connections = new();
    private readonly ConcurrentDictionary<Guid, int> _onlineCounts = new();

    public Task<PresenceChange> ConnectAsync(
        Guid userId,
        string connectionId,
        CancellationToken cancellationToken = default)
    {
        _connections[connectionId] = userId;
        var count = _onlineCounts.AddOrUpdate(userId, 1, (_, previous) => previous + 1);
        return Task.FromResult(new PresenceChange(userId, count == 1));
    }

    public Task<PresenceChange?> DisconnectAsync(
        string connectionId,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.TryRemove(connectionId, out var userId))
        {
            return Task.FromResult<PresenceChange?>(null);
        }

        var count = _onlineCounts.AddOrUpdate(userId, 0, (_, previous) => Math.Max(0, previous - 1));
        if (count > 0)
        {
            return Task.FromResult<PresenceChange?>(null);
        }

        _onlineCounts.TryRemove(userId, out _);
        return Task.FromResult<PresenceChange?>(new PresenceChange(userId, IsNowOnline: false));
    }

    public IReadOnlyList<Guid> GetOnlineUserIds()
        => _onlineCounts.Keys.ToList();
}