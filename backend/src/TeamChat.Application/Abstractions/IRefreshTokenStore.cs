using TeamChat.Domain.Entities;

namespace TeamChat.Application.Abstractions;

public interface IRefreshTokenStore
{
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default);

    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task UpdateAsync(RefreshToken token, CancellationToken cancellationToken = default);

    /// <summary>Revoque tous les refresh tokens actifs d'un utilisateur (rotation compromise, desactivation).</summary>
    Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
