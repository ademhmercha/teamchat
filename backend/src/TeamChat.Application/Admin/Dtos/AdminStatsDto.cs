namespace TeamChat.Application.Admin.Dtos;

/// <summary>Nombre de messages pour une journee donnee (dashboard admin).</summary>
public sealed record DailyMessageCountDto(DateOnly Date, int Count);

/// <summary>Groupe actif (nombre de messages sur la periode + nombre de membres).</summary>
public sealed record ActiveGroupDto(Guid ConversationId, string Name, int MessageCount, int MemberCount);

/// <summary>Statistiques agregees du tableau de bord admin (section 13).</summary>
public sealed record AdminStatsDto(
    int ActiveUsers,
    int TotalUsers,
    int ActiveGroups,
    int MessagesLast7Days,
    IReadOnlyList<DailyMessageCountDto> MessagesPerDay,
    IReadOnlyList<ActiveGroupDto> TopGroups);
