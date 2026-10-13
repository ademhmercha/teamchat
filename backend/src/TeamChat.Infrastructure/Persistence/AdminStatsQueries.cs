using Microsoft.EntityFrameworkCore;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Admin.Dtos;
using TeamChat.Domain.Entities;

namespace TeamChat.Infrastructure.Persistence;

/// <summary>Requetes agregees du tableau de bord admin (lecture seule).</summary>
public sealed class AdminStatsQueries : IAdminStatsQueries
{
    private readonly TeamChatDbContext _db;

    public AdminStatsQueries(TeamChatDbContext db)
    {
        _db = db;
    }

    public Task<int> CountUsersAsync(bool activeOnly, CancellationToken cancellationToken = default)
        => _db.Users.AsNoTracking()
            .CountAsync(u => !activeOnly || u.IsActive, cancellationToken);

    public Task<int> CountGroupsAsync(CancellationToken cancellationToken = default)
        => _db.Conversations.AsNoTracking()
            .CountAsync(c => c.Type == ConversationType.Group && c.ArchivedAt == null, cancellationToken);

    public async Task<IReadOnlyList<DailyMessageCountDto>> GetMessagesPerDayAsync(
        DateOnly fromInclusive,
        DateOnly toInclusive,
        CancellationToken cancellationToken = default)
    {
        var fromDate = StartOfDay(fromInclusive);
        var toExclusive = StartOfDay(toInclusive.AddDays(1));

        var timestamps = await _db.Messages.AsNoTracking()
            .Where(m => m.DeletedAt == null && m.CreatedAt >= fromDate && m.CreatedAt < toExclusive)
            .Select(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        var counts = timestamps
            .GroupBy(createdAt => DateOnly.FromDateTime(createdAt.UtcDateTime))
            .ToDictionary(group => group.Key, group => group.Count());

        var result = new List<DailyMessageCountDto>();
        for (var day = fromInclusive; day <= toInclusive; day = day.AddDays(1))
        {
            counts.TryGetValue(day, out var count);
            result.Add(new DailyMessageCountDto(day, count));
        }

        return result;
    }

    public async Task<IReadOnlyList<ActiveGroupDto>> GetTopGroupsAsync(
        DateOnly fromInclusive,
        int take,
        CancellationToken cancellationToken = default)
    {
        var fromDate = StartOfDay(fromInclusive);

        var rows = await (from m in _db.Messages.AsNoTracking()
                          join c in _db.Conversations.AsNoTracking() on m.ConversationId equals c.Id
                          where c.Type == ConversationType.Group
                                && c.ArchivedAt == null
                                && m.DeletedAt == null
                                && m.CreatedAt >= fromDate
                          group m by new { c.Id, c.Name } into g
                          orderby g.Count() descending
                          select new { g.Key.Id, g.Key.Name, Count = g.Count() })
            .Take(take)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return Array.Empty<ActiveGroupDto>();
        }

        var ids = rows.Select(r => r.Id).ToList();
        var memberCounts = await _db.ConversationParticipants.AsNoTracking()
            .Where(p => ids.Contains(p.ConversationId))
            .GroupBy(p => p.ConversationId)
            .Select(g => new { ConversationId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var members = memberCounts.ToDictionary(x => x.ConversationId, x => x.Count);

        return rows
            .Select(r => new ActiveGroupDto(
                r.Id,
                r.Name ?? "Groupe",
                r.Count,
                members.TryGetValue(r.Id, out var count) ? count : 0))
            .ToList();
    }

    private static DateTimeOffset StartOfDay(DateOnly day)
        => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
