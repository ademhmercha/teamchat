namespace TeamChat.Application.Users.Dtos;

public sealed record CreateUserRequest(string Email, string DisplayName, string Role, string Password);
