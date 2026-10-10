namespace TeamChat.Application.Abstractions;

/// <summary>
/// Profil minimal (role, IsActive, nom, avatar) mis en cache 1 min et verifie
/// a chaque requete authentifiee. Invalidation immediate a la desactivation.
/// </summary>
public sealed record UserAccessProfile(
    Guid Id,
    string DisplayName,
    string? AvatarUrl,
    bool IsActive,
    IReadOnlyList<string> Roles);

public interface IUserProfileCache
{
    Task<UserAccessProfile?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    ValueTask InvalidateAsync(Guid userId, CancellationToken cancellationToken = default);
}
