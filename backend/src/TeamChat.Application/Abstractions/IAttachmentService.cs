using TeamChat.Application.Attachments.Dtos;
using TeamChat.Application.Conversations.Dtos;

namespace TeamChat.Application.Abstractions;

public interface IAttachmentService
{
    Task<MessageDto> SaveAsync(
        Guid userId,
        Guid conversationId,
        AttachmentUpload upload,
        CancellationToken cancellationToken = default);

    Task<AttachmentDownload> OpenAsync(
        Guid userId,
        Guid attachmentId,
        CancellationToken cancellationToken = default);
}
