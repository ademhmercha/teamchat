using TeamChat.Application.Notifications.Dtos;
using TeamChat.Domain.Entities;

namespace TeamChat.Application.Abstractions;

/// <summary>Acces aux donnees des notifications (sans logique metier).</summary>
public interface INotificationQueries
{
    Task<NotificationDto> CreateAsync(
        Notification notification,
        CancellationToken cancellationToken = default);

    Task<NotificationListDto> GetForUserAsync(
        Guid userId,
        bool unreadOnly,
        int limit,
        CancellationToken cancellationToken = default);

    Task<bool> MarkReadAsync(
        Guid userId,
        Guid notificationId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<int> MarkAllReadAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<int> CountUnreadAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
