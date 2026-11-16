using Microsoft.AspNetCore.SignalR;
using TeamChat.Api.Hubs;
using TeamChat.Application.Abstractions;

namespace TeamChat.Api.Infrastructure;

public sealed class SignalRUserSessionTerminator : IUserSessionTerminator
{
    private readonly IHubContext<ChatHub, IChatClient> _hub;
    private readonly IPresenceTracker _presence;

    public SignalRUserSessionTerminator(
        IHubContext<ChatHub, IChatClient> hub,
        IPresenceTracker presence)
    {
        _hub = hub;
        _presence = presence;
    }

    public async Task TerminateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var connectionIds = _presence.GetConnectionIds(userId);

        await _hub.Clients.Group(ChatGroups.User(userId)).ForceLogout();

        var wentOffline = false;
        foreach (var connectionId in connectionIds)
        {
            var change = await _presence.DisconnectAsync(connectionId, cancellationToken);
            wentOffline |= change is not null;
        }

        if (wentOffline)
        {
            await _hub.Clients.All.PresenceChanged(userId, isOnline: false);
        }
    }
}
