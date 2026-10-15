using TeamChat.Application.Profile.Dtos;
using TeamChat.Application.Users.Dtos;

namespace TeamChat.Application.Abstractions;

/// <summary>Operations en libre-service sur le profil de l'utilisateur courant.</summary>
public interface IProfileService
{
    Task<UserProfileDto> UpdateAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default);

    Task ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default);

    Task LogoutAllAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<UserProfileDto> UploadAvatarAsync(
        Guid userId,
        AvatarUpload upload,
        string baseUrl,
        CancellationToken cancellationToken = default);

    Task RemoveAvatarAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<AvatarDownload> OpenAvatarAsync(Guid userId, CancellationToken cancellationToken = default);
}
