using TeamChat.Domain.Entities;

namespace TeamChat.Application.Notifications.Dtos;

/// <summary>Notification telle qu'affichee dans la cloche de l'application.</summary>
public sealed record NotificationDto(
    Guid Id,
    NotificationType Type,
    string Title,
    string Body,
    Guid? ConversationId,
    Guid? ActorUserId,
    string? ActorDisplayName,
    DateTimeOffset CreatedAt,
    bool Read);

public sealed record NotificationListDto(
    IReadOnlyList<NotificationDto> Items,
    int UnreadCount);
