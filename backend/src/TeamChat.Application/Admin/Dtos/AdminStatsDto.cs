namespace TeamChat.Application.Admin.Dtos;

public sealed record DailyMessageCountDto(DateOnly Date, int Count);

public sealed record ActiveGroupDto(Guid ConversationId, string Name, int MessageCount, int MemberCount);

public sealed record AdminStatsDto(
    int ActiveUsers,
    int TotalUsers,
    int ActiveGroups,
    int MessagesLast7Days,
    IReadOnlyList<DailyMessageCountDto> MessagesPerDay,
    IReadOnlyList<ActiveGroupDto> TopGroups);
