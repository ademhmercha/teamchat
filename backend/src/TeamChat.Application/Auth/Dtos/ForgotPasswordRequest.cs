namespace TeamChat.Application.Auth.Dtos;

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(Guid UserId, string Token, string Password);
