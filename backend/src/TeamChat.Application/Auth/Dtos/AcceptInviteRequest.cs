namespace TeamChat.Application.Auth.Dtos;

public sealed record AcceptInviteRequest(Guid UserId, string Token, string Password);
