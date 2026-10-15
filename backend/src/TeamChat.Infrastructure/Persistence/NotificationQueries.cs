using Microsoft.EntityFrameworkCore;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Notifications.Dtos;
using TeamChat.Domain.Entities;

namespace TeamChat.Infrastructure.Persistence;

/// <summary>Acces aux donnees des notifications.</summary>
public sealed class NotificationQueries : INotificationQueries
{
    private readonly TeamChatDbContext _db;

    public NotificationQueries(TeamChatDbContext db)
    {
        _db = db;
    }

    public async Task<NotificationDto> CreateAsync(
        Notification notification,
        CancellationToken cancellationToken = default)
    {
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(cancellationToken);

        string? actorName = null;
        if (notification.ActorUserId is { } actorId)
        {
            actorName = await _db.Users.AsNoTracking()
                .Where(u => u.Id == actorId)
                .Select(u => u.DisplayName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return ToDto(notification, actorName);
    }

    public async Task<NotificationListDto> GetForUserAsync(
        Guid userId,
        bool unreadOnly,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (unreadOnly)
        {
            query = query.Where(n => n.ReadAt == null);
        }

        var rows = await (from n in query
                          join u in _db.Users.AsNoTracking() on n.ActorUserId equals u.Id into actors
                          from u in actors.DefaultIfEmpty()
                          orderby n.CreatedAt descending
                          select new { Notification = n, ActorName = u == null ? null : u.DisplayName })
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync(cancellationToken);

        var unreadCount = await _db.Notifications.AsNoTracking()
            .CountAsync(n => n.UserId == userId && n.ReadAt == null, cancellationToken);

        return new NotificationListDto(
            rows.Select(r => ToDto(r.Notification, r.ActorName)).ToList(),
            unreadCount);
    }

    public async Task<bool> MarkReadAsync(
        Guid userId,
        Guid notificationId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var updated = await _db.Notifications
            .Where(n => n.Id == notificationId && n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.ReadAt, now), cancellationToken);

        return updated > 0;
    }

    public async Task<int> MarkAllReadAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
        => await _db.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.ReadAt, now), cancellationToken);

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default)
        => _db.Notifications.AsNoTracking()
            .CountAsync(n => n.UserId == userId && n.ReadAt == null, cancellationToken);

    private static NotificationDto ToDto(Notification notification, string? actorName)
        => new(
            notification.Id,
            notification.Type,
            notification.Title,
            notification.Body,
            notification.ConversationId,
            notification.ActorUserId,
            actorName,
            notification.CreatedAt,
            notification.ReadAt is not null);
}
