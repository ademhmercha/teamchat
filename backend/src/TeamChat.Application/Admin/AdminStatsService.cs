using TeamChat.Application.Abstractions;
using TeamChat.Application.Admin.Dtos;
using TeamChat.Application.Caching;

namespace TeamChat.Application.Admin;

/// <summary>
/// Agrege les statistiques du dashboard admin. Le resultat est mis en cache
/// 5 min (section 8 bis : donnees approximatives acceptees, pas d'invalidation).
/// </summary>
public sealed class AdminStatsService : IAdminStatsService
{
    private static readonly TimeSpan StatsTtl = TimeSpan.FromMinutes(5);
    private const int WindowDays = 7;
    private const int TopGroupCount = 5;

    private readonly IAdminStatsQueries _queries;
    private readonly ICacheService _cache;
    private readonly ICurrentTenant _tenant;
    private readonly TimeProvider _clock;

    public AdminStatsService(
        IAdminStatsQueries queries,
        ICacheService cache,
        ICurrentTenant tenant,
        TimeProvider clock)
    {
        _queries = queries;
        _cache = cache;
        _tenant = tenant;
        _clock = clock;
    }

    public Task<AdminStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
        => _cache
            .GetOrCreateAsync(CacheKeys.AdminStats(_tenant.TenantId), LoadAsync, StatsTtl, cancellationToken: cancellationToken)
            .AsTask();

    private async ValueTask<AdminStatsDto> LoadAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var from = today.AddDays(-(WindowDays - 1));

        var totalUsers = await _queries.CountUsersAsync(activeOnly: false, cancellationToken);
        var activeUsers = await _queries.CountUsersAsync(activeOnly: true, cancellationToken);
        var activeGroups = await _queries.CountGroupsAsync(cancellationToken);
        var perDay = await _queries.GetMessagesPerDayAsync(from, today, cancellationToken);
        var topGroups = await _queries.GetTopGroupsAsync(from, TopGroupCount, cancellationToken);

        var totalMessages = perDay.Sum(day => day.Count);

        return new AdminStatsDto(
            activeUsers,
            totalUsers,
            activeGroups,
            totalMessages,
            perDay,
            topGroups);
    }
}
