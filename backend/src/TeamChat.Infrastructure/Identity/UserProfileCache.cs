using TeamChat.Application.Abstractions;
using TeamChat.Application.Caching;

namespace TeamChat.Infrastructure.Identity;

/// <summary>
/// Cache du profil minimal d'un utilisateur (TTL 1 min). Un sentinelle non nul
/// permet de ne pas mettre en cache les utilisateurs inexistants tout en
/// respectant la contrainte HybridCache (valeur non nulle).
/// </summary>
public sealed class UserProfileCache : IUserProfileCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(1);
    private static readonly UserAccessProfile Missing = new(Guid.Empty, string.Empty, null, false, Array.Empty<string>());

    private readonly ICacheService _cache;
    private readonly IIdentityService _identity;
    private readonly ICurrentTenant _tenant;

    public UserProfileCache(ICacheService cache, IIdentityService identity, ICurrentTenant tenant)
    {
        _cache = cache;
        _identity = identity;
        _tenant = tenant;
    }

    public async Task<UserAccessProfile?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await _cache.GetOrCreateAsync(
            CacheKeys.User(_tenant.TenantId, userId),
            async token =>
            {
                var user = await _identity.FindByIdAsync(userId, token);
                if (user is null)
                {
                    return Missing;
                }

                var roles = await _identity.GetRolesAsync(user, token);
                return new UserAccessProfile(user.Id, user.DisplayName, user.AvatarUrl, user.IsActive, roles);
            },
            Ttl,
            new[] { CacheKeys.UserTag(userId) },
            cancellationToken);

        return profile.Id == Guid.Empty ? null : profile;
    }

    public ValueTask InvalidateAsync(Guid userId, CancellationToken cancellationToken = default)
        => _cache.RemoveByTagAsync(CacheKeys.UserTag(userId), cancellationToken);
}
