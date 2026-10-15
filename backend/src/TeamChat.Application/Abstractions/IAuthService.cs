using TeamChat.Application.Auth.Dtos;
using TeamChat.Application.Users.Dtos;

namespace TeamChat.Application.Abstractions;

public sealed record AuthResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserProfileDto User);

public interface IAuthService
{
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>Inscription publique : cree une societe et son compte proprietaire (Owner).</summary>
    Task<AuthResult> RegisterCompanyAsync(RegisterCompanyRequest request, CancellationToken cancellationToken = default);

    Task<AuthResult> LoginWithGoogleAsync(GoogleLoginRequest request, CancellationToken cancellationToken = default);

    Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task<UserProfileDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AcceptInviteAsync(AcceptInviteRequest request, CancellationToken cancellationToken = default);

    /// <summary>Envoie un lien de reinitialisation (sans reveler l'existence du compte).</summary>
    Task ForgotPasswordAsync(
        ForgotPasswordRequest request,
        string resetBaseUrl,
        CancellationToken cancellationToken = default);

    /// <summary>Definit un nouveau mot de passe a partir du token recu par email.</summary>
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
}
