using Microsoft.EntityFrameworkCore;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Conversations.Dtos;
using TeamChat.Domain.Entities;

namespace TeamChat.Infrastructure.Persistence;

/// <summary>Acces aux donnees des groupes (admin).</summary>
public sealed class AdminGroupQueries : IAdminGroupQueries
{
    private readonly TeamChatDbContext _db;

    public AdminGroupQueries(TeamChatDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<GroupDto>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        var groups = await _db.Conversations.AsNoTracking()
            .Where(c => c.Type == ConversationType.Group)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        if (groups.Count == 0)
        {
            return Array.Empty<GroupDto>();
        }

        var ids = groups.Select(g => g.Id).ToList();
        var members = await (from p in _db.ConversationParticipants.AsNoTracking()
                             join u in _db.Users.AsNoTracking() on p.UserId equals u.Id
                             where ids.Contains(p.ConversationId)
                             orderby p.JoinedAt
                             select new { p.ConversationId, u.Id, u.DisplayName, u.AvatarUrl })
            .ToListAsync(cancellationToken);

        return groups.Select(g => new GroupDto(
                g.Id,
                g.Name ?? string.Empty,
                g.CreatedAt,
                g.ArchivedAt is not null,
                members
                    .Where(m => m.ConversationId == g.Id)
                    .Select(m => new GroupMemberDto(m.Id, m.DisplayName, m.AvatarUrl))
                    .ToList()))
            .ToList();
    }

    public async Task<GroupDto?> FindGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var group = await _db.Conversations.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == groupId && c.Type == ConversationType.Group, cancellationToken);
        if (group is null)
        {
            return null;
        }

        var members = await (from p in _db.ConversationParticipants.AsNoTracking()
                             join u in _db.Users.AsNoTracking() on p.UserId equals u.Id
                             where p.ConversationId == groupId
                             orderby p.JoinedAt
                             select new GroupMemberDto(u.Id, u.DisplayName, u.AvatarUrl))
            .ToListAsync(cancellationToken);

        return new GroupDto(group.Id, group.Name ?? string.Empty, group.CreatedAt, group.ArchivedAt is not null, members);
    }

    public async Task<ConversationDto> CreateGroupAsync(
        Guid creatorId,
        string name,
        IReadOnlyList<Guid> memberIds,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Type = ConversationType.Group,
            Name = name,
            CreatedByUserId = creatorId,
            CreatedAt = now,
        };

        _db.Conversations.Add(conversation);
        _db.ConversationParticipants.AddRange(memberIds
            .Distinct()
            .Select(userId => new ConversationParticipant
            {
                ConversationId = conversation.Id,
                UserId = userId,
                JoinedAt = now,
            }));

        await _db.SaveChangesAsync(cancellationToken);

        var participants = await (from p in _db.ConversationParticipants.AsNoTracking()
                                  join u in _db.Users.AsNoTracking() on p.UserId equals u.Id
                                  where p.ConversationId == conversation.Id
                                  orderby p.JoinedAt
                                  select new ConversationParticipantDto(u.Id, u.DisplayName, u.AvatarUrl))
            .ToListAsync(cancellationToken);

        return new ConversationDto(conversation.Id, conversation.Type, conversation.Name, conversation.CreatedAt, participants);
    }

    public async Task<GroupOperationResult> SetMembersAsync(
        Guid groupId,
        IReadOnlyList<Guid> memberIds,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var desired = memberIds.Distinct().ToHashSet();

        var current = await _db.ConversationParticipants
            .Where(p => p.ConversationId == groupId)
            .ToListAsync(cancellationToken);
        var currentIds = current.Select(p => p.UserId).ToHashSet();

        var toAdd = desired.Except(currentIds).ToList();
        var toRemove = current.Where(p => !desired.Contains(p.UserId)).ToList();

        _db.ConversationParticipants.RemoveRange(toRemove);
        _db.ConversationParticipants.AddRange(toAdd.Select(userId => new ConversationParticipant
        {
            ConversationId = groupId,
            UserId = userId,
            JoinedAt = now,
        }));

        await _db.SaveChangesAsync(cancellationToken);

        var groupName = await _db.Conversations
            .Where(c => c.Id == groupId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        return new GroupOperationResult(groupId, groupName, toAdd, toRemove.Select(p => p.UserId).ToList(), desired.ToList());
    }

    public async Task ArchiveAsync(Guid groupId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var conversation = await _db.Conversations.FirstAsync(c => c.Id == groupId, cancellationToken);
        conversation.ArchivedAt = now;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RenameAsync(Guid groupId, string name, CancellationToken cancellationToken = default)
    {
        var conversation = await _db.Conversations.FirstAsync(c => c.Id == groupId, cancellationToken);
        conversation.Name = name;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UnarchiveAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var conversation = await _db.Conversations.FirstAsync(c => c.Id == groupId, cancellationToken);
        conversation.ArchivedAt = null;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var conversation = await _db.Conversations.FirstAsync(c => c.Id == groupId, cancellationToken);
        _db.Conversations.Remove(conversation);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetParticipantIdsAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
        => await _db.ConversationParticipants.AsNoTracking()
            .Where(p => p.ConversationId == conversationId)
            .Select(p => p.UserId)
            .ToListAsync(cancellationToken);

    public Task<bool> GroupExistsAsync(Guid groupId, CancellationToken cancellationToken = default)
        => _db.Conversations.AsNoTracking()
            .AnyAsync(c => c.Id == groupId && c.Type == ConversationType.Group, cancellationToken);

    public Task<int> CountExistingUsersAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken = default)
        => _db.Users.AsNoTracking().CountAsync(u => userIds.Contains(u.Id), cancellationToken);
}
