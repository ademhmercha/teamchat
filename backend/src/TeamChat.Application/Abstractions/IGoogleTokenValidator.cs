namespace TeamChat.Application.Abstractions;

public sealed record GoogleUserInfo(string Email, string? DisplayName, string? PictureUrl);

public interface IGoogleTokenValidator
{
    Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}
