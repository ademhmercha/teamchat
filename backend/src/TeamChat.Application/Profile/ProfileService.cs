using FluentValidation;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Attachments;
using TeamChat.Application.Common;
using TeamChat.Application.Profile.Dtos;
using TeamChat.Application.Users.Dtos;
using TeamChat.Domain.Entities;

namespace TeamChat.Application.Profile;

public sealed class ProfileService : IProfileService
{
    private const int MaxAvatarBytes = 2 * 1024 * 1024;
    private const int MaxDisplayNameLength = 100;

    private readonly IIdentityService _identity;
    private readonly IRefreshTokenStore _refreshTokens;
    private readonly IUserProfileCache _profileCache;
    private readonly IFileStorage _storage;

    public ProfileService(
        IIdentityService identity,
        IRefreshTokenStore refreshTokens,
        IUserProfileCache profileCache,
        IFileStorage storage)
    {
        _identity = identity;
        _refreshTokens = refreshTokens;
        _profileCache = profileCache;
        _storage = storage;
    }

    public async Task<UserProfileDto> UpdateAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(userId, cancellationToken);
        var displayName = request.DisplayName.Trim();
        if (displayName.Length is 0 or > MaxDisplayNameLength)
        {
            throw new BadRequestException("Le nom affiche est invalide.");
        }

        user.DisplayName = displayName;

        var result = await _identity.UpdateAsync(user, cancellationToken);
        if (!result.Succeeded)
        {
            throw new ValidationException(result.Errors.Select(e =>
                new FluentValidation.Results.ValidationFailure(nameof(request.DisplayName), e)));
        }

        await _profileCache.InvalidateAsync(userId, cancellationToken);

        var roles = await _identity.GetRolesAsync(user, cancellationToken);
        return ToProfile(user, roles);
    }

    public async Task ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(userId, cancellationToken);

        var check = await _identity.CheckPasswordAsync(user, request.CurrentPassword, lockoutOnFailure: false, cancellationToken);
        if (check != SignInOutcome.Success)
        {
            throw new BadRequestException("Mot de passe actuel incorrect.");
        }

        var result = await _identity.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword, cancellationToken);
        if (!result.Succeeded)
        {
            throw new ValidationException(result.Errors.Select(e =>
                new FluentValidation.Results.ValidationFailure(nameof(request.NewPassword), e)));
        }

        // Un changement de mot de passe invalide toutes les sessions existantes.
        await _refreshTokens.RevokeAllForUserAsync(userId, cancellationToken);
        await _profileCache.InvalidateAsync(userId, cancellationToken);
    }

    public Task LogoutAllAsync(Guid userId, CancellationToken cancellationToken = default)
        => _refreshTokens.RevokeAllForUserAsync(userId, cancellationToken);

    public async Task<UserProfileDto> UploadAvatarAsync(
        Guid userId,
        AvatarUpload upload,
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(userId, cancellationToken);

        if (upload.Length <= 0)
        {
            throw new BadRequestException("Le fichier est vide.");
        }

        if (upload.Length > MaxAvatarBytes)
        {
            throw new BadRequestException($"L'avatar ne peut pas depasser {MaxAvatarBytes / (1024 * 1024)} Mo.");
        }

        var data = await ReadAllAsync(upload.Content, MaxAvatarBytes, cancellationToken);

        var detected = FileSignatureDetector.Detect(data, upload.ContentType, upload.FileName, isVoice: false);
        if (detected is null || detected.Kind != AttachmentKind.Image)
        {
            throw new BadRequestException("Format d'image non supporte (PNG, JPEG, GIF ou WEBP).");
        }

        await _storage.SaveAsync(StorageKey(userId), new MemoryStream(data, writable: false), detected.ContentType, cancellationToken);

        user.AvatarUrl = $"{baseUrl.TrimEnd('/')}/api/users/{userId}/avatar";

        var result = await _identity.UpdateAsync(user, cancellationToken);
        if (!result.Succeeded)
        {
            throw new ValidationException(result.Errors.Select(e =>
                new FluentValidation.Results.ValidationFailure(nameof(upload.FileName), e)));
        }

        await _profileCache.InvalidateAsync(userId, cancellationToken);

        var roles = await _identity.GetRolesAsync(user, cancellationToken);
        return ToProfile(user, roles);
    }

    public async Task RemoveAvatarAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(userId, cancellationToken);
        if (user.AvatarUrl is null)
        {
            return;
        }

        await SafeDeleteAsync(StorageKey(userId), cancellationToken);

        user.AvatarUrl = null;
        await _identity.UpdateAsync(user, cancellationToken);
        await _profileCache.InvalidateAsync(userId, cancellationToken);
    }

    public async Task<AvatarDownload> OpenAvatarAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _identity.FindByIdAsync(userId, cancellationToken);
        if (user?.AvatarUrl is null)
        {
            throw new NotFoundException("Avatar introuvable.");
        }

        Stream stream;
        try
        {
            stream = await _storage.OpenReadAsync(StorageKey(userId), cancellationToken);
        }
        catch (FileNotFoundException)
        {
            throw new NotFoundException("Avatar introuvable.");
        }

        var data = await ReadAllAsync(stream, MaxAvatarBytes, cancellationToken);
        var detected = FileSignatureDetector.Detect(data, null, string.Empty, isVoice: false);

        return new AvatarDownload(new MemoryStream(data, writable: false), detected?.ContentType ?? "application/octet-stream");
    }

    private async Task<AppUser> FindAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _identity.FindByIdAsync(userId, cancellationToken);
        if (user is null || user.DeletedAt is not null)
        {
            throw new NotFoundException("Utilisateur introuvable.");
        }

        return user;
    }

    private static string StorageKey(Guid userId) => $"avatar_{userId:N}";

    private static async Task<byte[]> ReadAllAsync(Stream content, long maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        long total = 0;

        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                throw new BadRequestException("Le fichier est trop volumineux.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        return buffer.ToArray();
    }

    private async Task SafeDeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        try
        {
            await _storage.DeleteAsync(storageKey, cancellationToken);
        }
        catch
        {
            // Compensation best-effort : ne masque pas l'operation principale.
        }
    }

    private static UserProfileDto ToProfile(AppUser user, IReadOnlyList<string> roles) => new(
        user.Id,
        user.DisplayName,
        user.Email ?? string.Empty,
        user.AvatarUrl,
        roles.FirstOrDefault() ?? string.Empty,
        user.IsActive);
}
