using Microsoft.EntityFrameworkCore;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Conversations.Dtos;
using TeamChat.Domain.Entities;

namespace TeamChat.Infrastructure.Persistence;

/// <summary>Acces aux donnees des pieces jointes.</summary>
public sealed class AttachmentQueries : IAttachmentQueries
{
    private readonly TeamChatDbContext _db;

    public AttachmentQueries(TeamChatDbContext db)
    {
        _db = db;
    }

    public async Task<MessageDto> PersistAttachmentMessageAsync(
        Guid conversationId,
        Guid senderId,
        string storageKey,
        string originalFileName,
        string contentType,
        long sizeBytes,
        int? durationSec,
        MessageType type,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var attachment = new Attachment
        {
            Id = Guid.NewGuid(),
            StorageKey = storageKey,
            OriginalFileName = originalFileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            DurationSec = durationSec,
            UploadedByUserId = senderId,
            CreatedAt = now,
        };

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderId = senderId,
            Type = type,
            Content = null,
            AttachmentId = attachment.Id,
            CreatedAt = now,
        };

        _db.Attachments.Add(attachment);
        _db.Messages.Add(message);
        await _db.SaveChangesAsync(cancellationToken);

        var sender = await _db.Users.AsNoTracking()
            .Where(u => u.Id == senderId)
            .Select(u => new { u.DisplayName, u.AvatarUrl })
            .FirstOrDefaultAsync(cancellationToken);

        return new MessageDto(
            message.Sequence,
            message.Id,
            message.ConversationId,
            message.SenderId,
            sender?.DisplayName ?? string.Empty,
            sender?.AvatarUrl,
            message.Type,
            message.Content,
            message.AttachmentId,
            message.CreatedAt,
            Edited: false,
            Deleted: false,
            Attachment: new MessageAttachmentDto(
                attachment.Id,
                attachment.OriginalFileName,
                attachment.ContentType,
                attachment.SizeBytes,
                attachment.DurationSec));
    }

    public async Task<AttachmentDownloadInfo?> FindForDownloadAsync(
        Guid attachmentId,
        CancellationToken cancellationToken = default)
        => await (from m in _db.Messages.AsNoTracking()
                  join a in _db.Attachments.AsNoTracking() on m.AttachmentId equals a.Id
                  where a.Id == attachmentId && m.DeletedAt == null
                  select new AttachmentDownloadInfo(
                      a.Id,
                      m.ConversationId,
                      a.StorageKey,
                      a.OriginalFileName,
                      a.ContentType,
                      a.SizeBytes))
            .FirstOrDefaultAsync(cancellationToken);
}
