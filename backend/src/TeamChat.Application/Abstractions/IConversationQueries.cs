using TeamChat.Application.Conversations.Dtos;

namespace TeamChat.Application.Abstractions;

public interface IConversationQueries
{
    Task<ConversationDto?> FindDirectConversationAsync(
        Guid userA,
        Guid userB,
        CancellationToken cancellationToken = default);

    Task<ConversationDto> CreateDirectAsync(
        Guid requestorId,
        Guid otherUserId,
        CancellationToken cancellationToken = default);

    Task<MessageDto> AddMessageAsync(
        Guid conversationId,
        Guid senderId,
        string content,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<MessageDto> AddSystemMessageAsync(
        Guid conversationId,
        Guid senderId,
        string content,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<MessageHistoryResult> GetMessagesAsync(
        Guid conversationId,
        long? before,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConversationListItemDto>> GetConversationsForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetMemberConversationIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<MessageDto?> FindMessageAsync(
        Guid messageId,
        CancellationToken cancellationToken = default);

    Task<MessageDto> UpdateMessageAsync(
        Guid messageId,
        string content,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<MessageDto> SoftDeleteMessageAsync(
        Guid messageId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<Guid?> GetLastMessageIdAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task SetLastReadAsync(
        Guid conversationId,
        Guid userId,
        Guid? messageId,
        CancellationToken cancellationToken = default);

    Task<ConversationDetailDto?> GetConversationDetailAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConversationParticipantDto>> GetMembersAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<bool> LeaveAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task SetArchivedAsync(
        Guid conversationId,
        Guid userId,
        bool archived,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SharedAttachmentDto>> GetSharedAttachmentsAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<MessageHistoryResult> SearchMessagesAsync(
        Guid conversationId,
        string query,
        int limit,
        CancellationToken cancellationToken = default);
}