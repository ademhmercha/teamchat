using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TeamChat.Application.Abstractions;
using TeamChat.Infrastructure.Caching;

namespace TeamChat.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Enregistre les services d'infrastructure : cache (HybridCache + Redis optionnel),
    /// puis EF Core / Identity / stockage dans les sprints suivants.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));

        var redisConnection = configuration["Redis:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddStackExchangeRedisCache(options => options.Configuration = redisConnection);
        }

        services.AddHybridCache();
        services.AddSingleton<ICacheService, HybridCacheService>();

        return services;
    }
}
