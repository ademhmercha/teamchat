using TeamChat.Application.Tenants.Dtos;

namespace TeamChat.Application.Abstractions;

/// <summary>Gestion de la societe de l'utilisateur courant (cote Owner).</summary>
public interface ICompanyService
{
    /// <summary>
    /// Renomme la societe a laquelle appartient l'utilisateur. Le compte doit
    /// etre rattache a une societe (jamais le cas du super-admin).
    /// </summary>
    Task<TenantDto> RenameAsync(Guid userId, string name, CancellationToken cancellationToken = default);
}
