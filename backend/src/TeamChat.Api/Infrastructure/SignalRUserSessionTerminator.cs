using Microsoft.AspNetCore.SignalR;
using TeamChat.Api.Hubs;
using TeamChat.Application.Abstractions;

namespace TeamChat.Api.Infrastructure;

/// <summary>
/// Ferme les sessions temps reel d'un utilisateur : diffusion de l'ordre de
/// deconnexion aux clients, retrait des connexions du suivi de presence et
/// notification du passage hors-ligne aux autres utilisateurs.
/// </summary>
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
