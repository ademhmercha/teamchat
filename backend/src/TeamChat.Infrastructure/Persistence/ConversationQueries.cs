using Microsoft.EntityFrameworkCore;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Caching;
using TeamChat.Application.Common;
using TeamChat.Application.Conversations.Dtos;
using TeamChat.Domain.Entities;

namespace TeamChat.Infrastructure.Persistence;

/// <summary>Acces aux donnees des conversations.</summary>
public sealed class ConversationQueries : IConversationQueries
{
    private static readonly TimeSpan DirectMembershipTtl = TimeSpan.FromMinutes(5);

    private readonly TeamChatDbContext _db;
    private readonly ICacheService _cache;

    public ConversationQueries(TeamChatDbContext db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<ConversationDto?> FindDirectConversationAsync(
        Guid userA,
        Guid userB,
        CancellationToken cancellationToken = default)
    {
        var conversationId = await _db.Conversations.AsNoTracking()
            .Where(c => c.Type == ConversationType.Direct && c.DirectKey == BuildDirectKey(userA, userB))
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return conversationId is null
            ? null
            : await BuildConversationDtoAsync(conversationId.Value, cancellationToken);
    }

    public async Task<ConversationDto> CreateDirectAsync(
        Guid requestorId,
        Guid otherUserId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Type = ConversationType.Direct,
            CreatedByUserId = requestorId,
            CreatedAt = now,
            DirectKey = BuildDirectKey(requestorId, otherUserId),
        };

        _db.Conversations.Add(conversation);
        _db.ConversationParticipants.AddRange(
            new ConversationParticipant { ConversationId = conversation.Id, UserId = requestorId, JoinedAt = now },
            new ConversationParticipant { ConversationId = conversation.Id, UserId = otherUserId, JoinedAt = now });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (conversation is { DirectKey: not null })
        {
            // Course possible entre deux creations simultanees : l'index
            // unique sur DirectKey protege l'unicite, on recupere l'existante.
            var existing = await _db.Conversations.AsNoTracking()
                .FirstOrDefaultAsync(c => c.DirectKey == conversation.DirectKey, cancellationToken);
            if (existing is not null)
            {
                await _cache.RemoveByTagAsync(CacheKeys.MembershipTag(existing.Id), cancellationToken);
                return await BuildConversationDtoAsync(existing.Id, cancellationToken);
            }

            throw;
        }

        return await BuildConversationDtoAsync(conversation.Id, cancellationToken);
    }

    public async Task<MessageDto> AddMessageAsync(
        Guid conversationId,
        Guid senderId,
        string content,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderId = senderId,
            Type = MessageType.Text,
            Content = content,
            CreatedAt = now,
        };

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
            Deleted: false);
    }

    public async Task<MessageHistoryResult> GetMessagesAsync(
        Guid conversationId,
        long? before,
        int limit,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Message> query = _db.Messages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId);

        if (before is not null)
        {
            query = query.Where(m => m.Sequence < before.Value);
        }

        var rows = await (from m in query
                          join u in _db.Users.AsNoTracking() on m.SenderId equals u.Id into members
                          from u in members.DefaultIfEmpty()
                          join a in _db.Attachments.AsNoTracking() on m.AttachmentId equals a.Id into attachments
                          from a in attachments.DefaultIfEmpty()
                          orderby m.Sequence descending
                          select new
                          {
                              Message = m,
                              SenderName = u == null ? string.Empty : u.DisplayName,
                              SenderAvatar = u == null ? null : u.AvatarUrl,
                              Attachment = a,
                          })
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > limit;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var items = rows
            .Select(r => new MessageDto(
                r.Message.Sequence,
                r.Message.Id,
                r.Message.ConversationId,
                r.Message.SenderId,
                r.SenderName,
                r.SenderAvatar,
                r.Message.Type,
                r.Message.DeletedAt is null ? r.Message.Content : null,
                r.Message.AttachmentId,
                r.Message.CreatedAt,
                Edited: r.Message.EditedAt is not null,
                Deleted: r.Message.DeletedAt is not null,
                Attachment: r.Message.DeletedAt is null ? ToAttachmentDto(r.Attachment) : null))
            .ToList();

        MessageCursor? nextCursor = items.Count == 0
            ? null
            : new MessageCursor(items[^1].Sequence);

        return new MessageHistoryResult(items, nextCursor, hasMore);
    }

    public async Task<IReadOnlyList<ConversationListItemDto>> GetConversationsForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var conversations = await _db.Conversations.AsNoTracking()
            .Where(c => c.ArchivedAt == null &&
                        _db.ConversationParticipants.Any(p => p.ConversationId == c.Id && p.UserId == userId))
            .ToListAsync(cancellationToken);

        if (conversations.Count == 0)
        {
            return Array.Empty<ConversationListItemDto>();
        }

        var conversationIds = conversations.Select(c => c.Id).ToList();

        var others = await (from p in _db.ConversationParticipants.AsNoTracking()
                            join u in _db.Users.AsNoTracking() on p.UserId equals u.Id
                            where conversationIds.Contains(p.ConversationId) && p.UserId != userId
                            select new { p.ConversationId, u.Id, u.DisplayName, u.AvatarUrl })
            .ToListAsync(cancellationToken);

        var participantCounts = await _db.ConversationParticipants.AsNoTracking()
            .Where(p => conversationIds.Contains(p.ConversationId))
            .GroupBy(p => p.ConversationId)
            .Select(g => new { ConversationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ConversationId, x => x.Count, cancellationToken);

        var myReads = await _db.ConversationParticipants.AsNoTracking()
            .Where(p => p.UserId == userId && conversationIds.Contains(p.ConversationId))
            .Select(p => new { p.ConversationId, p.LastReadMessageId })
            .ToDictionaryAsync(x => x.ConversationId, x => x.LastReadMessageId, cancellationToken);

        var messageRows = await _db.Messages.AsNoTracking()
            .Where(m => conversationIds.Contains(m.ConversationId))
            .Select(m => new MessageRow(m.Id, m.ConversationId, m.Sequence, m.SenderId, m.CreatedAt, m.Type, m.Content, m.DeletedAt))
            .ToListAsync(cancellationToken);

        var sequenceById = messageRows.ToDictionary(m => m.Id, m => m.Sequence);
        var lastMessages = messageRows
            .GroupBy(m => m.ConversationId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.Sequence).First());

        return conversations
            .Select(c =>
            {
                var other = others.FirstOrDefault(p => p.ConversationId == c.Id);
                var last = lastMessages.GetValueOrDefault(c.Id);
                var lastReadSequence = ResolveSequence(myReads.GetValueOrDefault(c.Id), sequenceById);
                var unread = messageRows.Count(m =>
                    m.ConversationId == c.Id &&
                    m.SenderId != userId &&
                    m.DeletedAt is null &&
                    m.Sequence > lastReadSequence);

                return new ConversationListItemDto(
                    c.Id,
                    c.Type,
                    c.Name,
                    other?.Id,
                    other?.DisplayName,
                    other?.AvatarUrl,
                    last?.CreatedAt,
                    BuildPreview(last),
                    participantCounts.GetValueOrDefault(c.Id),
                    unread);
            })
            .OrderByDescending(item => item.LastMessageAt ?? DateTimeOffset.MinValue)
            .ToList();
    }

    public async Task<IReadOnlyList<Guid>> GetMemberConversationIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
        => await _db.ConversationParticipants.AsNoTracking()
            .Where(p => p.UserId == userId &&
                        _db.Conversations.Any(c => c.Id == p.ConversationId && c.ArchivedAt == null))
            .Select(p => p.ConversationId)
            .ToListAsync(cancellationToken);

    public async Task<MessageDto?> FindMessageAsync(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        var row = await (from m in _db.Messages.AsNoTracking()
                         join u in _db.Users.AsNoTracking() on m.SenderId equals u.Id into members
                         from u in members.DefaultIfEmpty()
                         join a in _db.Attachments.AsNoTracking() on m.AttachmentId equals a.Id into attachments
                         from a in attachments.DefaultIfEmpty()
                         where m.Id == messageId
                         select new
                         {
                             Message = m,
                             SenderName = u == null ? string.Empty : u.DisplayName,
                             SenderAvatar = u == null ? null : u.AvatarUrl,
                             Attachment = a,
                         })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : ToMessageDto(row.Message, row.SenderName, row.SenderAvatar, row.Attachment);
    }

    public async Task<MessageDto> UpdateMessageAsync(
        Guid messageId,
        string content,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var message = await _db.Messages.FirstAsync(m => m.Id == messageId, cancellationToken);
        message.Content = content;
        message.EditedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        return await RequireMessageDtoAsync(messageId, cancellationToken);
    }

    public async Task<MessageDto> SoftDeleteMessageAsync(
        Guid messageId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var message = await _db.Messages.FirstAsync(m => m.Id == messageId, cancellationToken);
        message.DeletedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        var dto = await RequireMessageDtoAsync(messageId, cancellationToken);
        return dto with { Content = null, Deleted = true };
    }

    public async Task<Guid?> GetLastMessageIdAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
        => await _db.Messages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.Sequence)
            .Select(m => (Guid?)m.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task SetLastReadAsync(
        Guid conversationId,
        Guid userId,
        Guid? messageId,
        CancellationToken cancellationToken = default)
    {
        if (messageId is null)
        {
            return;
        }

        var participant = await _db.ConversationParticipants
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == userId, cancellationToken);
        if (participant is null)
        {
            return;
        }

        var targetSequence = await _db.Messages.AsNoTracking()
            .Where(m => m.Id == messageId && m.ConversationId == conversationId)
            .Select(m => (long?)m.Sequence)
            .FirstOrDefaultAsync(cancellationToken);
        if (targetSequence is null)
        {
            return;
        }

        if (participant.LastReadMessageId is { } currentId)
        {
            var currentSequence = await _db.Messages.AsNoTracking()
                .Where(m => m.Id == currentId)
                .Select(m => (long?)m.Sequence)
                .FirstOrDefaultAsync(cancellationToken);
            if (currentSequence is not null && currentSequence >= targetSequence)
            {
                return;
            }
        }

        participant.LastReadMessageId = messageId;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<MessageDto> RequireMessageDtoAsync(Guid messageId, CancellationToken cancellationToken)
        => await FindMessageAsync(messageId, cancellationToken)
           ?? throw new NotFoundException("Message introuvable.");

    private static long ResolveSequence(Guid? messageId, IReadOnlyDictionary<Guid, long> sequenceById)
        => messageId is { } id && sequenceById.TryGetValue(id, out var sequence) ? sequence : 0;

    private static MessageDto ToMessageDto(Message message, string senderName, string? senderAvatar, TeamChat.Domain.Entities.Attachment? attachment = null)
        => new(
            message.Sequence,
            message.Id,
            message.ConversationId,
            message.SenderId,
            senderName,
            senderAvatar,
            message.Type,
            message.DeletedAt is null ? message.Content : null,
            message.AttachmentId,
            message.CreatedAt,
            Edited: message.EditedAt is not null,
            Deleted: message.DeletedAt is not null,
            Attachment: message.DeletedAt is null ? ToAttachmentDto(attachment) : null);

    private static MessageAttachmentDto? ToAttachmentDto(TeamChat.Domain.Entities.Attachment? attachment)
        => attachment is null
            ? null
            : new MessageAttachmentDto(
                attachment.Id,
                attachment.OriginalFileName,
                attachment.ContentType,
                attachment.SizeBytes,
                attachment.DurationSec);

    private sealed record MessageRow(
        Guid Id,
        Guid ConversationId,
        long Sequence,
        Guid SenderId,
        DateTimeOffset CreatedAt,
        MessageType Type,
        string? Content,
        DateTimeOffset? DeletedAt);

    private static string? BuildPreview(object? message)
        => message switch
        {
            MessageRow row => BuildPreview(row.Type, row.Content, row.DeletedAt),
            Message msg => BuildPreview(msg.Type, msg.Content, msg.DeletedAt),
            _ => null,
        };

    private static string? BuildPreview(MessageType type, string? content, DateTimeOffset? deletedAt)
    {
        if (deletedAt is not null)
        {
            return "Message supprime";
        }

        var text = type switch
        {
            MessageType.Voice => "Message vocal",
            MessageType.Image => "Image",
            MessageType.File => "Fichier",
            _ => content ?? string.Empty,
        };

        return text.Length <= 60 ? text : text[..60] + "...";
    }

    private async Task<ConversationDto> BuildConversationDtoAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var conversation = await _db.Conversations.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken)
            ?? throw new NotFoundException("Conversation introuvable.");

        var participants = await (from p in _db.ConversationParticipants.AsNoTracking()
                                  join u in _db.Users.AsNoTracking() on p.UserId equals u.Id
                                  where p.ConversationId == conversationId
                                  orderby p.JoinedAt
                                  select new ConversationParticipantDto(u.Id, u.DisplayName, u.AvatarUrl))
            .ToListAsync(cancellationToken);

        return new ConversationDto(
            conversation.Id,
            conversation.Type,
            conversation.Name,
            conversation.CreatedAt,
            participants);
    }

    private static string BuildDirectKey(Guid userA, Guid userB)
    {
        var (min, max) = userA.CompareTo(userB) <= 0 ? (userA, userB) : (userB, userA);
        return $"d:{min:D}:{max:D}";
    }
}