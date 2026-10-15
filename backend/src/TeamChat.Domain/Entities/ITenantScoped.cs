namespace TeamChat.Domain.Entities;

/// <summary>
/// Marque une entite rattachee a une societe (tenant). Le <c>TeamChatDbContext</c>
/// applique un filtre global sur <see cref="TenantId"/> et renseigne
/// automatiquement la societe courante a la creation.
/// </summary>
public interface ITenantScoped
{
    Guid TenantId { get; set; }
}
