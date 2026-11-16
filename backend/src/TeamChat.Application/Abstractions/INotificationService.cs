using TeamChat.Application.Notifications.Dtos;
using TeamChat.Domain.Entities;

namespace TeamChat.Application.Abstractions;

public interface INotificationService
{
    Task<NotificationDto> NotifyGroupAddedAsync(
        Guid userId,
        Guid conversationId,
        string groupName,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<NotificationDto> NotifyMissedCallAsync(
        Guid calleeId,
        Guid callerId,
        CallType type,
        CancellationToken cancellationToken = default);

    Task<NotificationListDto> GetMineAsync(
        Guid userId,
        bool unreadOnly,
        int limit,
        CancellationToken cancellationToken = default);

    Task<bool> MarkReadAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default);

    Task MarkAllReadAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
