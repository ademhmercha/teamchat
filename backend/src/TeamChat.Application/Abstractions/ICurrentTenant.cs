namespace TeamChat.Application.Abstractions;

/// <summary>
/// Societe (tenant) de la requete courante, derivee du jeton JWT.
/// <see cref="FilterDisabled"/> vaut vrai pour les contextes sans utilisateur
/// authentifie (login, refresh, inscription, seed) : le filtre global est alors
/// desactive, ce qui permet les recherches par email a l'authentification.
/// </summary>
public interface ICurrentTenant
{
    /// <summary>Societe courante, ou null pour le super-admin (hors societe).</summary>
    Guid? TenantId { get; }

    /// <summary>Vrai si le filtre global par societe doit etre ignore.</summary>
    bool FilterDisabled { get; }
}

/// <summary>
/// Permet d'imposer la societe courante en dehors d'une requete HTTP classique
/// (invocations du hub SignalR, ou <see cref="Microsoft.AspNetCore.Http.IHttpContextAccessor"/>
/// n'est pas renseigne). L'implementation est scoped : la valeur n'affecte que
/// le scope courant.
/// </summary>
public interface ICurrentTenantSetter
{
    void SetTenant(Guid? tenantId);
}
