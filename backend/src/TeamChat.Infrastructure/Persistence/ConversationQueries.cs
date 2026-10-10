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
                          orderby m.Sequence descending
                          select new { Message = m, SenderName = u == null ? string.Empty : u.DisplayName, SenderAvatar = u == null ? null : u.AvatarUrl })
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
                Deleted: r.Message.DeletedAt is not null))
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
            .Where(c => c.Type == ConversationType.Direct &&
                        _db.ConversationParticipants.Any(p => p.ConversationId == c.Id && p.UserId == userId))
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        if (conversations.Count == 0)
        {
            return Array.Empty<ConversationListItemDto>();
        }

        var conversationIds = conversations.Select(c => c.Id).ToList();

        var participants = await (from p in _db.ConversationParticipants.AsNoTracking()
                                  join u in _db.Users.AsNoTracking() on p.UserId equals u.Id
                                  where conversationIds.Contains(p.ConversationId) && p.UserId != userId
                                  select new { p.ConversationId, u.Id, u.DisplayName, u.AvatarUrl })
            .ToListAsync(cancellationToken);

        var messages = await _db.Messages.AsNoTracking()
            .Where(m => conversationIds.Contains(m.ConversationId))
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .ToListAsync(cancellationToken);
        var lastMessages = messages.GroupBy(m => m.ConversationId).ToDictionary(g => g.Key, g => g.First());

        return conversations
            .Select(c =>
            {
                var other = participants.FirstOrDefault(p => p.ConversationId == c.Id);
                var last = lastMessages.GetValueOrDefault(c.Id);

                return new ConversationListItemDto(
                    c.Id,
                    c.Type,
                    c.Name,
                    other?.Id,
                    other?.DisplayName,
                    other?.AvatarUrl,
                    last?.CreatedAt,
                    BuildPreview(last));
            })
            .ToList();
    }

    public async Task<IReadOnlyList<Guid>> GetMemberConversationIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var ids = await _db.ConversationParticipants.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.ConversationId)
            .ToListAsync(cancellationToken);

        return ids;
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

    private static string? BuildPreview(Message? message)
    {
        if (message is null)
        {
            return null;
        }

        if (message.DeletedAt is not null)
        {
            return "Message supprime";
        }

        var content = message.Content ?? string.Empty;
        return content.Length <= 60 ? content : content[..60] + "...";
    }
}