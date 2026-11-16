using TeamChat.Application.Conversations.Dtos;
using TeamChat.Domain.Entities;

namespace TeamChat.Application.Abstractions;

public interface IAttachmentQueries
{
    Task<MessageDto> PersistAttachmentMessageAsync(
        Guid conversationId,
        Guid senderId,
        string storageKey,
        string originalFileName,
        string contentType,
        long sizeBytes,
        int? durationSec,
        MessageType type,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<AttachmentDownloadInfo?> FindForDownloadAsync(
        Guid attachmentId,
        CancellationToken cancellationToken = default);
}

public sealed record AttachmentDownloadInfo(
    Guid AttachmentId,
    Guid ConversationId,
    string StorageKey,
    string FileName,
    string ContentType,
    long SizeBytes);
