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

    Task<AuthResult> LoginWithGoogleAsync(GoogleLoginRequest request, CancellationToken cancellationToken = default);

    Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task<UserProfileDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AcceptInviteAsync(AcceptInviteRequest request, CancellationToken cancellationToken = default);
}
