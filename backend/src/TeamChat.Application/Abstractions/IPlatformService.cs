using TeamChat.Application.Platform.Dtos;

namespace TeamChat.Application.Abstractions;

/// <summary>Administration plateforme des societes (reservee au super-admin).</summary>
public interface IPlatformService
{
    Task<IReadOnlyList<PlatformTenantDto>> GetTenantsAsync(CancellationToken cancellationToken = default);

    Task<PlatformTenantDto> GetTenantAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PlatformTenantDto> UpdateTenantAsync(
        Guid id,
        UpdateTenantRequest request,
        CancellationToken cancellationToken = default);
}
