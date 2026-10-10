namespace TeamChat.IntegrationTests.Support;

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    UserProfileDto? User);

public sealed record UserProfileDto(
    Guid Id,
    string DisplayName,
    string Email,
    string? AvatarUrl,
    string Role,
    bool IsActive);

public sealed record UserListItemDto(
    Guid Id,
    string DisplayName,
    string Email,
    string? AvatarUrl,
    bool IsActive);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);