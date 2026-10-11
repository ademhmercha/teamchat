using Microsoft.EntityFrameworkCore;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Caching;
using TeamChat.Infrastructure.Persistence;

namespace TeamChat.Infrastructure.Security;

/// <summary>
/// Appartenance a une conversation avec cache (5 min) et invalidation par
/// conversation. Periodiquement invalide pour refleter les changements
/// de participants (section 8 bis).
/// </summary>
public sealed class MembershipService : IMembershipService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private readonly TeamChatDbContext _db;
    private readonly ICacheService _cache;

    public MembershipService(TeamChatDbContext db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public Task<bool> IsMemberAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => _cache.GetOrCreateAsync(
            CacheKeys.Membership(conversationId, userId),
            token => HasMembershipAsync(conversationId, userId, token),
            Ttl,
            new[] { CacheKeys.MembershipTag(conversationId) },
            cancellationToken).AsTask();

    public Task InvalidateConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
        => _cache.RemoveByTagAsync(CacheKeys.MembershipTag(conversationId), cancellationToken).AsTask();

    private ValueTask<bool> HasMembershipAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken)
        => new(_db.ConversationParticipants.AsNoTracking()
            .AnyAsync(
                p => p.ConversationId == conversationId
                     && p.UserId == userId
                     && _db.Conversations.Any(c => c.Id == p.ConversationId && c.ArchivedAt == null),
                cancellationToken));
}