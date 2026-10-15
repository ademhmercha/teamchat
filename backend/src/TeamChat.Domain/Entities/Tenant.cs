namespace TeamChat.Domain.Entities;

/// <summary>
/// Societe (tenant). Chaque societe isole ses utilisateurs, conversations,
/// messages, pieces jointes, appels et notifications des autres societes.
/// </summary>
public sealed class Tenant
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Identifiant lisible et unique (derive du nom).</summary>
    public string Slug { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
