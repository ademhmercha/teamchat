using Microsoft.AspNetCore.Identity;

namespace TeamChat.Domain.Entities;

/// <summary>
/// Utilisateur applicatif. Herite de IdentityUser pour reutiliser la gestion
/// des mots de passe, du verrouillage et des roles par ASP.NET Core Identity.
/// </summary>
public class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastSeenAt { get; set; }
}
