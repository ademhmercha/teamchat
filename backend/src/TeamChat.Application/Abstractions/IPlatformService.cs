using TeamChat.Application.Platform.Dtos;

namespace TeamChat.Application.Abstractions;

public interface IPlatformService
{
    Task<IReadOnlyList<PlatformTenantDto>> GetTenantsAsync(CancellationToken cancellationToken = default);

    Task<PlatformTenantDto> GetTenantAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PlatformTenantDto> UpdateTenantAsync(
        Guid id,
        UpdateTenantRequest request,
        CancellationToken cancellationToken = default);
}
