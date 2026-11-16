using TeamChat.Application.Admin.Dtos;

namespace TeamChat.Application.Abstractions;

public interface IAdminStatsService
{
    Task<AdminStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);
}
