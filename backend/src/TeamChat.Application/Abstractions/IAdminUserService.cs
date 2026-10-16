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

    /// <summary>Cree un utilisateur actif avec le mot de passe fourni par l'administrateur (aucun email).</summary>
    Task<UserProfileDto> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Met a jour le nom, le role et l'activation. La desactivation invalide le cache et revoque les tokens.</summary>
    Task<UserProfileDto> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Supprime logiquement un compte : anonymise le profil, interdit la connexion,
    /// revoque les sessions et conserve les messages (auteur « Utilisateur supprime »).
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Definit directement un nouveau mot de passe pour un compte (aucun email).</summary>
    Task ResetPasswordAsync(
        Guid id,
        ResetUserPasswordRequest request,
        CancellationToken cancellationToken = default);
}
