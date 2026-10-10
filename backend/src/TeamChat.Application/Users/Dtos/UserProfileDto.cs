namespace TeamChat.Application.Users.Dtos;

public sealed record UserProfileDto(
    Guid Id,
    string DisplayName,
    string Email,
    string? AvatarUrl,
    string Role,
    bool IsActive);
