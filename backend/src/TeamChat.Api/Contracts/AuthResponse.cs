using TeamChat.Application.Users.Dtos;

namespace TeamChat.Api.Contracts;

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    UserProfileDto User);
