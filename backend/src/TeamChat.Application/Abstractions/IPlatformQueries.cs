using TeamChat.Application.Platform.Dtos;

namespace TeamChat.Application.Abstractions;

public interface IPlatformQueries
{
    Task<IReadOnlyList<PlatformTenantDto>> GetTenantsAsync(CancellationToken cancellationToken = default);

    Task<PlatformTenantDto?> GetTenantAsync(Guid id, CancellationToken cancellationToken = default);
}
