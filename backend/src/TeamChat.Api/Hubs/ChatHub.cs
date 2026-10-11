using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TeamChat.Api.Infrastructure;
using TeamChat.Application.Abstractions;

namespace TeamChat.Api.Hubs;

/// <summary>
/// Hub de messagerie (WebSockets, /hubs/chat). L'authentification se fait via
/// le JWT passe en query string (access_token) par le client SignalR.
/// Aucune logique metier ici : le hub appelle les services et relaye.
/// </summary>
[Authorize]
public sealed class ChatHub : Hub<IChatClient>
{
    private readonly IConversationService _conversations;
    private readonly IMembershipService _membership;
    private readonly IPresenceTracker _presence;
    private readonly IUserProfileCache _profiles;

    public ChatHub(
        IConversationService conversations,
        IMembershipService membership,
        IPresenceTracker presence,
        IUserProfileCache profiles)
    {
        _conversations = conversations;
        _membership = membership;
        _presence = presence;
        _profiles = profiles;
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