using TeamChat.Application.Abstractions;
using TeamChat.Application.Notifications.Dtos;
using TeamChat.Domain.Entities;

namespace TeamChat.Application.Notifications;

public sealed class NotificationService : INotificationService
{
    private readonly INotificationQueries _queries;
    private readonly IUserProfileCache _profiles;
    private readonly TimeProvider _time;

    public NotificationService(
        INotificationQueries queries,
        IUserProfileCache profiles,
        TimeProvider time)
    {
        _queries = queries;
        _profiles = profiles;
        _time = time;
    }

    public async Task<NotificationDto> NotifyGroupAddedAsync(
        Guid userId,
        Guid conversationId,
        string groupName,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = NotificationType.GroupAdded,
            Title = "Nouveau groupe",
            Body = $"Vous avez ete ajoute au groupe « {groupName} ».",
            ConversationId = conversationId,
            ActorUserId = actorUserId,
            CreatedAt = _time.GetUtcNow(),
        };

        return await _queries.CreateAsync(notification, cancellationToken);
    }

    public async Task<NotificationDto> NotifyMissedCallAsync(
        Guid calleeId,
        Guid callerId,
        CallType type,
        CancellationToken cancellationToken = default)
    {
        var caller = await _profiles.GetAsync(callerId, cancellationToken);
        var label = type == CallType.Video ? "video" : "audio";

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = calleeId,
            Type = NotificationType.MissedCall,
            Title = "Appel manque",
            Body = $"Appel {label} manque de {caller?.DisplayName ?? "un contact"}.",
            ActorUserId = callerId,
            CreatedAt = _time.GetUtcNow(),
        };

        return await _queries.CreateAsync(notification, cancellationToken);
    }

    public Task<NotificationListDto> GetMineAsync(
        Guid userId,
        bool unreadOnly,
        int limit,
        CancellationToken cancellationToken = default)
        => _queries.GetForUserAsync(userId, unreadOnly, limit, cancellationToken);

    public Task<bool> MarkReadAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
        => _queries.MarkReadAsync(userId, notificationId, _time.GetUtcNow(), cancellationToken);

    public Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default)
        => _queries.MarkAllReadAsync(userId, _time.GetUtcNow(), cancellationToken);
}
