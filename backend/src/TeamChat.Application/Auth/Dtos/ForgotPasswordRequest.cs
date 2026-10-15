namespace TeamChat.Application.Auth.Dtos;

/// <summary>Demande de reinitialisation du mot de passe (mot de passe oublie).</summary>
public sealed record ForgotPasswordRequest(string Email);

/// <summary>Definition d'un nouveau mot de passe a partir du token recu par email.</summary>
public sealed record ResetPasswordRequest(Guid UserId, string Token, string Password);
