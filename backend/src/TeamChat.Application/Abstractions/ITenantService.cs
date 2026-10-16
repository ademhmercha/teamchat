using TeamChat.Application.Tenants.Dtos;

namespace TeamChat.Application.Abstractions;

/// <summary>Gestion des societes (creation, lecture).</summary>
public interface ITenantService
{
    Task<TenantDto> CreateAsync(string name, CancellationToken cancellationToken = default);

    Task<TenantDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Identifiants des societes actives (utilise par les traitements d'arriere-plan par societe).</summary>
    Task<IReadOnlyList<Guid>> GetActiveIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>Vrai si la societe existe et est active (null si l'utilisateur est hors societe).</summary>
    Task<bool> IsActiveAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Renomme et/ou active/desactive une societe (super-admin).</summary>
    Task UpdateAsync(
        Guid id,
        string? name,
        bool? isActive,
        CancellationToken cancellationToken = default);
}
