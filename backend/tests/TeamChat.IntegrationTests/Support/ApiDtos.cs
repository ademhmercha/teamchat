namespace TeamChat.IntegrationTests.Support;

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    UserProfileDto? User);

public sealed record UserProfileDto(
    Guid Id,
    string DisplayName,
    string Email,
    string? AvatarUrl,
    string Role,
    bool IsActive);

public sealed record UserListItemDto(
    Guid Id,
    string DisplayName,
    string Email,
    string? AvatarUrl,
    bool IsActive);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record ConversationParticipantDto(
    Guid UserId,
    string DisplayName,
    string? AvatarUrl);

public sealed record ConversationDto(
    Guid Id,
    int Type,
    string? Name,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ConversationParticipantDto> Participants);

public sealed record ConversationListItemDto(
    Guid Id,
    int Type,
    string? Name,
    Guid? OtherUserId,
    string? OtherDisplayName,
    string? OtherAvatarUrl,
    DateTimeOffset? LastMessageAt,
    string? LastMessagePreview,
    int ParticipantCount,
    int UnreadCount);

public sealed record MessageAttachmentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    int? DurationSec);

public sealed record MessageDto(
    long Sequence,
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    string SenderDisplayName,
    string? SenderAvatarUrl,
    int Type,
    string? Content,
    Guid? AttachmentId,
    DateTimeOffset CreatedAt,
    bool Edited,
    bool Deleted,
    MessageAttachmentDto? Attachment = null);

public sealed record MessageCursor(long Sequence);

public sealed record MessageHistoryResult(
    IReadOnlyList<MessageDto> Items,
    MessageCursor? NextCursor,
    bool HasMore);

public sealed record GroupMemberDto(Guid UserId, string DisplayName, string? AvatarUrl);

public sealed record GroupDto(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    bool IsArchived,
    IReadOnlyList<GroupMemberDto> Members);

public sealed record CallSessionDto(
    Guid CallId,
    Guid CallerId,
    string CallerDisplayName,
    Guid CalleeId,
    string CalleeDisplayName,
    int Type,
    int Status,
    Guid ConversationId,
    DateTimeOffset StartedAt);

public sealed record IceServerDto(
    IReadOnlyList<string> Urls,
    string? Username,
    string? Credential);

public sealed record CallHistoryItemDto(
    Guid Id,
    Guid OtherUserId,
    string OtherDisplayName,
    int Type,
    int Status,
    DateTimeOffset StartedAt,
    int? DurationSeconds,
    bool Outgoing);

public sealed record DailyMessageCountDto(DateOnly Date, int Count);

public sealed record ActiveGroupDto(Guid ConversationId, string Name, int MessageCount, int MemberCount);

public sealed record AdminStatsDto(
    int ActiveUsers,
    int TotalUsers,
    int ActiveGroups,
    int MessagesLast7Days,
    IReadOnlyList<DailyMessageCountDto> MessagesPerDay,
    IReadOnlyList<ActiveGroupDto> TopGroups);