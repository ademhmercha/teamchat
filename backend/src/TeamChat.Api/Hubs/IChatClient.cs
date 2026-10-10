using TeamChat.Application.Conversations.Dtos;

namespace TeamChat.Api.Hubs;

public interface IChatClient
{
    Task ReceiveMessage(MessageDto message);

    Task UserTyping(Guid conversationId, Guid userId, string displayName);

    Task UserStoppedTyping(Guid conversationId, Guid userId);

    Task PresenceChanged(Guid userId, bool isOnline);

    Task PresenceSnapshot(IReadOnlyList<Guid> onlineUserIds);
}