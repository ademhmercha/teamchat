using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using TeamChat.Application.Abstractions;

namespace TeamChat.Infrastructure.Caching;

/// <summary>
/// Implementation de <see cref="ICacheService"/> basee sur HybridCache.
/// - L1 memoire locale toujours active.
/// - L2 Redis active uniquement si Redis:ConnectionString est renseignee (voir DependencyInjection).
/// HybridCache protege nativement contre le cache stampede.
/// </summary>
public sealed class HybridCacheService : ICacheService
{
    private readonly HybridCache _cache;
    private readonly TimeSpan _defaultTtl;

    public HybridCacheService(HybridCache cache, IOptions<CacheOptions> options)
    {
        _cache = cache;
        _defaultTtl = TimeSpan.FromSeconds(Math.Max(1, options.Value.DefaultTtlSeconds));
    }

    public ValueTask<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        TimeSpan? ttl = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        var entryOptions = new HybridCacheEntryOptions
        {
            Expiration = ttl ?? _defaultTtl,
        };

        return _cache.GetOrCreateAsync(key, factory, entryOptions, tags, cancellationToken);
    }

    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _cache.RemoveAsync(key, cancellationToken);
    }

    public ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        return _cache.RemoveByTagAsync(tag, cancellationToken);
    }
}
