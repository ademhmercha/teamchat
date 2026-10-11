using TeamChat.Application.Conversations.Dtos;

namespace TeamChat.Api.Hubs;

public interface IChatClient
{
    Task ReceiveMessage(MessageDto message);

    Task MessageEdited(MessageDto message);

    Task MessageDeleted(Guid conversationId, Guid messageId);

    Task UserTyping(Guid conversationId, Guid userId, string displayName);

    Task UserStoppedTyping(Guid conversationId, Guid userId);

    Task PresenceChanged(Guid userId, bool isOnline);

    Task PresenceSnapshot(IReadOnlyList<Guid> onlineUserIds);

    /// <summary>L'utilisateur vient d'etre ajoute a une conversation de groupe.</summary>
    Task AddedToGroup(Guid conversationId);

    /// <summary>La liste des conversations a change (membres, archivage, nouveau message).</summary>
    Task ConversationUpdated(Guid conversationId);
}
