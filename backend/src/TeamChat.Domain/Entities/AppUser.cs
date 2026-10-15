using Microsoft.AspNetCore.Identity;

namespace TeamChat.Domain.Entities;

/// <summary>
/// Utilisateur applicatif. Herite de IdentityUser pour reutiliser la gestion
/// des mots de passe, du verrouillage et des roles par ASP.NET Core Identity.
/// </summary>
public class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Societe de rattachement. Null uniquement pour le super-admin de la
    /// plateforme (qui n'appartient a aucune societe).
    /// </summary>
    public Guid? TenantId { get; set; }

    public string? AvatarUrl { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Date de suppression logique du compte. Non nul = compte anonymise
    /// (nom, email et avatar effaces, connexion interdite) mais ses messages
    /// sont conserves avec l'auteur « Utilisateur supprime ».
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastSeenAt { get; set; }
}
