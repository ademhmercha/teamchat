namespace TeamChat.Infrastructure.Caching;

/// <summary>Options liees a la section "Cache" de la configuration.</summary>
public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>TTL par defaut (secondes) applique quand aucun TTL explicite n'est fourni.</summary>
    public int DefaultTtlSeconds { get; set; } = 60;
}
