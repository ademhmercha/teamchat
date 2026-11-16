using TeamChat.Application.Conversations.Dtos;

namespace TeamChat.Application.Abstractions;

public interface IConversationService
{
    Task<ConversationDto> GetOrCreateDirectAsync(
        Guid userId,
        Guid otherUserId,
        CancellationToken cancellationToken = default);

    Task<MessageDto> SendMessageAsync(
        Guid userId,
        Guid conversationId,
        string content,
        CancellationToken cancellationToken = default);

    Task<MessageHistoryResult> GetHistoryAsync(
        Guid userId,
        Guid conversationId,
        long? before,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConversationListItemDto>> GetMyConversationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetMemberConversationIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<MessageDto> EditMessageAsync(
        Guid userId,
        Guid messageId,
        string newContent,
        CancellationToken cancellationToken = default);

    Task<MessageDto> DeleteMessageAsync(
        Guid userId,
        Guid messageId,
        CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(
        Guid userId,
        Guid conversationId,
        Guid? messageId,
        CancellationToken cancellationToken = default);

    Task<ConversationDetailDto> GetDetailAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConversationParticipantDto>> GetMembersAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<LeaveConversationResult> LeaveAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task SetArchivedAsync(
        Guid userId,
        Guid conversationId,
        bool archived,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SharedAttachmentDto>> GetSharedAttachmentsAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<MessageHistoryResult> SearchMessagesAsync(
        Guid userId,
        Guid conversationId,
        string query,
        int limit,
        CancellationToken cancellationToken = default);
}