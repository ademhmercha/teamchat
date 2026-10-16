using TeamChat.Application.Platform.Dtos;

namespace TeamChat.Application.Abstractions;

/// <summary>
/// Lecture transverse de toutes les societes (vue plateforme). L'implementation
/// ignore volontairement le filtre global par societe : elle est reservee au
/// super-admin et ne doit jamais etre exposee aux utilisateurs d'une societe.
/// </summary>
public interface IPlatformQueries
{
    Task<IReadOnlyList<PlatformTenantDto>> GetTenantsAsync(CancellationToken cancellationToken = default);

    Task<PlatformTenantDto?> GetTenantAsync(Guid id, CancellationToken cancellationToken = default);
}
