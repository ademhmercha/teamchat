namespace TeamChat.Application.Users.Dtos;

public sealed record UserListItemDto(
    Guid Id,
    string DisplayName,
    string Email,
    string? AvatarUrl,
    bool IsActive);
