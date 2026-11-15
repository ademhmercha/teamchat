using TeamChat.Application.Admin.Dtos;

namespace TeamChat.Application.Abstractions;

public interface IAdminStatsQueries
{
    Task<int> CountUsersAsync(bool activeOnly, CancellationToken cancellationToken = default);

    Task<int> CountGroupsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyMessageCountDto>> GetMessagesPerDayAsync(
        DateOnly fromInclusive,
        DateOnly toInclusive,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActiveGroupDto>> GetTopGroupsAsync(
        DateOnly fromInclusive,
        int take,
        CancellationToken cancellationToken = default);
}
