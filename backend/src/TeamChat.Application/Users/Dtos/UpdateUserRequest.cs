namespace TeamChat.Application.Users.Dtos;

public sealed record UpdateUserRequest(string? DisplayName, string? Role, bool? IsActive);
