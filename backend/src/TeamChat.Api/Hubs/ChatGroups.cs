namespace TeamChat.Api.Hubs;

/// <summary>Noms des groupes SignalR partages entre le hub et les controllers.</summary>
public static class ChatGroups
{
    private const string ConversationPrefix = "conversation:";
    private const string UserPrefix = "user:";

    public static string Conversation(Guid conversationId) => $"{ConversationPrefix}{conversationId:D}";

    public static string User(Guid userId) => $"{UserPrefix}{userId:D}";
}
