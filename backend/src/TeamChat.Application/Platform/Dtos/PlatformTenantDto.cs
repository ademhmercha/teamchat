namespace TeamChat.Application.Platform.Dtos;

public sealed record PlatformTenantDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    DateTimeOffset CreatedAt,
    int UserCount,
    int ActiveUserCount,
    int GroupCount,
    int MessageCount);

public sealed record UpdateTenantRequest(string? Name, bool? IsActive);
