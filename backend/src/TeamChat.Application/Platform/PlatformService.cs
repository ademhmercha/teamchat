using TeamChat.Application.Abstractions;
using TeamChat.Application.Common;
using TeamChat.Application.Platform.Dtos;

namespace TeamChat.Application.Platform;

/// <summary>
/// Administration plateforme : liste les societes et les met a jour.
/// Les compteurs transverses sont fournis par <see cref="IPlatformQueries"/>,
/// qui ignore le filtre global par societe (reserve au super-admin).
/// </summary>
public sealed class PlatformService : IPlatformService
{
    private const int MaxNameLength = 100;

    private readonly IPlatformQueries _queries;
    private readonly ITenantService _tenants;

    public PlatformService(IPlatformQueries queries, ITenantService tenants)
    {
        _queries = queries;
        _tenants = tenants;
    }

    public Task<IReadOnlyList<PlatformTenantDto>> GetTenantsAsync(CancellationToken cancellationToken = default)
        => _queries.GetTenantsAsync(cancellationToken);

    public async Task<PlatformTenantDto> GetTenantAsync(Guid id, CancellationToken cancellationToken = default)
        => await _queries.GetTenantAsync(id, cancellationToken)
           ?? throw new NotFoundException("Societe introuvable.");

    public async Task<PlatformTenantDto> UpdateTenantAsync(
        Guid id,
        UpdateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        string? name = null;
        if (request.Name is not null)
        {
            name = request.Name.Trim();
            if (name.Length is 0 or > MaxNameLength)
            {
                throw new BadRequestException("Le nom de la societe est invalide.");
            }
        }

        if (request.Name is null && request.IsActive is null)
        {
            throw new BadRequestException("Aucun champ a mettre a jour.");
        }

        await _tenants.UpdateAsync(id, name, request.IsActive, cancellationToken);
        return await GetTenantAsync(id, cancellationToken);
    }
}
