namespace TeamChat.Application.Abstractions;

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
