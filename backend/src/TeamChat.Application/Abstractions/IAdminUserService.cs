using TeamChat.Application.Common;
using TeamChat.Application.Users.Dtos;

namespace TeamChat.Application.Abstractions;

public interface IAdminUserService
{
    /// <summary>Liste tous les comptes (actifs et desactives) avec leur role.</summary>
    Task<PagedResult<UserProfileDto>> ListAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Cree un utilisateur et envoie une invitation (lien contenant un token de definition de mot de passe).</summary>
    Task<UserProfileDto> CreateAsync(
        CreateUserRequest request,
        string inviteBaseUrl,
        CancellationToken cancellationToken = default);

    /// <summary>Met a jour le nom, le role et l'activation. La desactivation invalide le cache et revoque les tokens.</summary>
    Task<UserProfileDto> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default);
}
