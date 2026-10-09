namespace TeamChat.Application.Abstractions;

/// <summary>
/// Abstraction de cache utilisee par la couche Application.
/// L'implementation (HybridCache : memoire L1 + Redis L2 optionnel) vit dans Infrastructure.
/// Le code metier ignore quel backend est utilise.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Retourne la valeur en cache pour <paramref name="key"/>, ou la calcule via
    /// <paramref name="factory"/> puis la stocke avec le TTL demande (defaut : Cache:DefaultTtlSeconds).
    /// </summary>
    ValueTask<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        TimeSpan? ttl = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default);

    /// <summary>Supprime une entree de cache par sa cle.</summary>
    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Supprime toutes les entrees associees a un tag (invalidation groupee).</summary>
    ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default);
}
