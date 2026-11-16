namespace TeamChat.Api.Hubs;

public static class ChatGroups
{
    private const string ConversationPrefix = "conversation:";
    private const string UserPrefix = "user:";

    public static string Conversation(Guid conversationId) => $"{ConversationPrefix}{conversationId:D}";

    public static string User(Guid userId) => $"{UserPrefix}{userId:D}";
}
