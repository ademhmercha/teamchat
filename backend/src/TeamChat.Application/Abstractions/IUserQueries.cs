using TeamChat.Application.Common;
using TeamChat.Application.Users.Dtos;

namespace TeamChat.Application.Abstractions;

/// <summary>Lecture de l'annuaire (cote requetes). L'implementation applique le cache (60 s).</summary>
public interface IUserQueries
{
    Task<PagedResult<UserListItemDto>> SearchAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<UserProfileDto?> GetProfileAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Liste administrateur : inclut les comptes desactives et le role (hors comptes supprimes).</summary>
    Task<PagedResult<UserProfileDto>> AdminSearchAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Nombre d'administrateurs actifs et non supprimes (garde du dernier admin).</summary>
    Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken = default);
}
