using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TeamChat.Api.Infrastructure;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Calls.Dtos;
using TeamChat.Application.Common;
using TeamChat.Domain.Entities;

namespace TeamChat.Api.Hubs;

/// <summary>
/// Hub de messagerie et de signalisation d'appels (WebSockets, /hubs/chat).
/// L'authentification se fait via le JWT passe en query string (access_token)
/// par le client SignalR. Aucune logique metier ici : le hub appelle les
/// services et relaye (y compris la signalisation WebRTC vers le pair).
/// </summary>
[Authorize]
public sealed class ChatHub : Hub<IChatClient>
{
    private readonly IConversationService _conversations;
    private readonly IMembershipService _membership;
    private readonly IPresenceTracker _presence;
    private readonly IUserProfileCache _profiles;
    private readonly ICallService _calls;

    public ChatHub(
        IConversationService conversations,
        IMembershipService membership,
        IPresenceTracker presence,
        IUserProfileCache profiles,
        ICallService calls)
    {
        _conversations = conversations;
        _membership = membership;
        _presence = presence;
        _profiles = profiles;
        _calls = calls;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetCurrentUserId();

        await Groups.AddToGroupAsync(Context.ConnectionId, ChatGroups.User(userId));
        foreach (var conversationId in await _conversations.GetMemberConversationIdsAsync(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, ChatGroups.Conversation(conversationId));
        }

        var change = await _presence.ConnectAsync(userId, Context.ConnectionId);
        if (change.IsNowOnline)
        {
            await Clients.Others.PresenceChanged(userId, isOnline: true);
        }

        await Clients.Caller.PresenceSnapshot(_presence.GetOnlineUserIds());

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var change = await _presence.DisconnectAsync(Context.ConnectionId);
        if (change is not null)
        {
            await Clients.All.PresenceChanged(change.UserId, isOnline: false);

            var outcome = await _calls.EndCallForDisconnectedUserAsync(change.UserId);
            if (outcome is not null)
            {
                await NotifyCallEndedAsync(outcome);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(Guid conversationId, string content)
    {
        var userId = GetCurrentUserId();

        if (!await IsMemberAsync(conversationId, userId))
        {
            throw new HubException("Vous ne faites pas partie de cette conversation.");
        }

        var message = await _conversations.SendMessageAsync(userId, conversationId, content);
        await Clients.Group(ChatGroups.Conversation(conversationId)).ReceiveMessage(message);
    }

    public async Task EditMessage(Guid messageId, string newContent)
    {
        var message = await _conversations.EditMessageAsync(GetCurrentUserId(), messageId, newContent);
        await Clients.Group(ChatGroups.Conversation(message.ConversationId)).MessageEdited(message);
    }

    public async Task DeleteMessage(Guid messageId)
    {
        var message = await _conversations.DeleteMessageAsync(GetCurrentUserId(), messageId);
        await Clients.Group(ChatGroups.Conversation(message.ConversationId)).MessageDeleted(message.ConversationId, message.Id);
    }

    public async Task MarkAsRead(Guid conversationId, Guid messageId)
    {
        await _conversations.MarkAsReadAsync(GetCurrentUserId(), conversationId, messageId);
    }

    public async Task StartTyping(Guid conversationId)
    {
        var userId = GetCurrentUserId();

        if (!await IsMemberAsync(conversationId, userId))
        {
            return;
        }

        var profile = await _profiles.GetAsync(userId);
        await Clients.OthersInGroup(ChatGroups.Conversation(conversationId))
            .UserTyping(conversationId, userId, profile?.DisplayName ?? string.Empty);
    }

    public async Task StopTyping(Guid conversationId)
    {
        var userId = GetCurrentUserId();

        if (!await IsMemberAsync(conversationId, userId))
        {
            return;
        }

        await Clients.OthersInGroup(ChatGroups.Conversation(conversationId))
            .UserStoppedTyping(conversationId, userId);
    }

    /// <summary>Ouvre un appel vers un utilisateur (audio par defaut).</summary>
    public async Task<CallSessionDto> CallUser(Guid calleeId, CallType type)
    {
        var callerId = GetCurrentUserId();
        var session = await CallAsync(() => _calls.StartCallAsync(callerId, calleeId, type));

        // La conversation directe peut avoir ete creee a l'instant : ses messages
        // systeme sont diffuses au groupe de conversation, il faut donc y inscrire
        // les connexions deja ouvertes des deux participants.
        foreach (var connectionId in _presence.GetConnectionIds(callerId))
        {
            await Groups.AddToGroupAsync(connectionId, ChatGroups.Conversation(session.ConversationId));
        }

        foreach (var connectionId in _presence.GetConnectionIds(calleeId))
        {
            await Groups.AddToGroupAsync(connectionId, ChatGroups.Conversation(session.ConversationId));
        }

        await Clients.Group(ChatGroups.User(calleeId)).IncomingCall(session);
        return session;
    }

    public async Task AcceptCall(Guid callId)
    {
        var session = await CallAsync(() => _calls.AcceptCallAsync(GetCurrentUserId(), callId));
        await Clients.Group(ChatGroups.User(session.CallerId)).CallAccepted(session);
    }

    public async Task RejectCall(Guid callId)
    {
        var outcome = await CallAsync(() => _calls.RejectCallAsync(GetCurrentUserId(), callId));
        await NotifyCallEndedAsync(outcome);
    }

    public async Task EndCall(Guid callId)
    {
        var outcome = await CallAsync(() => _calls.EndCallAsync(GetCurrentUserId(), callId));
        await NotifyCallEndedAsync(outcome);
    }

    public async Task SendOffer(Guid callId, string sdp)
    {
        var relay = await CallAsync(() => _calls.AuthorizeRelayAsync(GetCurrentUserId(), callId));
        await Clients.Group(ChatGroups.User(relay.PeerUserId)).ReceiveOffer(callId, sdp);
    }

    public async Task SendAnswer(Guid callId, string sdp)
    {
        var relay = await CallAsync(() => _calls.AuthorizeRelayAsync(GetCurrentUserId(), callId));
        await Clients.Group(ChatGroups.User(relay.PeerUserId)).ReceiveAnswer(callId, sdp);
    }

    public async Task SendIceCandidate(Guid callId, string candidate, string? sdpMid, int? sdpMLineIndex)
    {
        var relay = await CallAsync(() => _calls.AuthorizeRelayAsync(GetCurrentUserId(), callId));
        await Clients.Group(ChatGroups.User(relay.PeerUserId)).ReceiveIceCandidate(callId, candidate, sdpMid, sdpMLineIndex);
    }

    private static async Task<T> CallAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (AppException exception)
        {
            throw new HubException(exception.Message);
        }
    }

    private async Task NotifyCallEndedAsync(CallOutcomeDto outcome)
    {
        await Clients.Group(ChatGroups.User(outcome.CallerId)).CallEnded(outcome.CallId, outcome.Status);
        await Clients.Group(ChatGroups.User(outcome.CalleeId)).CallEnded(outcome.CallId, outcome.Status);

        if (outcome.SystemMessage is not null)
        {
            await Clients.Group(ChatGroups.Conversation(outcome.SystemMessage.ConversationId))
                .ReceiveMessage(outcome.SystemMessage);
        }
    }

    private async Task<bool> IsMemberAsync(Guid conversationId, Guid userId)
        => await _membership.IsMemberAsync(conversationId, userId);

    private Guid GetCurrentUserId()
    {
        if (Context.User is null)
        {
            throw new UnauthorizedAccessException("Non authentifie.");
        }

        return Context.User.GetUserId();
    }
}
