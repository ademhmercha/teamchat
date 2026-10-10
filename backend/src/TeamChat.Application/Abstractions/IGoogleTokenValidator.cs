namespace TeamChat.Application.Abstractions;

public sealed record GoogleUserInfo(string Email, string? DisplayName, string? PictureUrl);

public interface IGoogleTokenValidator
{
    /// <summary>Valide un ID token Google cote serveur (audience = notre Client ID). Retourne null si invalide.</summary>
    Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}
