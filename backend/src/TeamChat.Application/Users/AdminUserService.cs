using FluentValidation;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Caching;
using TeamChat.Application.Common;
using TeamChat.Application.Users.Dtos;
using TeamChat.Domain.Constants;
using TeamChat.Domain.Entities;

namespace TeamChat.Application.Users;

public sealed class AdminUserService : IAdminUserService
{
    private readonly IIdentityService _identity;
    private readonly IRefreshTokenStore _refreshTokens;
    private readonly IUserProfileCache _profileCache;
    private readonly ICacheService _cache;
    private readonly IEmailSender _email;

    public AdminUserService(
        IIdentityService identity,
        IRefreshTokenStore refreshTokens,
        IUserProfileCache profileCache,
        ICacheService cache,
        IEmailSender email)
    {
        _identity = identity;
        _refreshTokens = refreshTokens;
        _profileCache = profileCache;
        _cache = cache;
        _email = email;
    }

    public async Task<UserProfileDto> CreateAsync(
        CreateUserRequest request,
        string inviteBaseUrl,
        CancellationToken cancellationToken = default)
    {
        if (!Roles.All.Contains(request.Role))
        {
            throw new ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure(nameof(request.Role), "Role invalide."),
            });
        }

        if (await _identity.FindByEmailAsync(request.Email, cancellationToken) is not null)
        {
            throw new ConflictException("Un utilisateur avec cet email existe deja.");
        }

        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        EnsureSuccess(await _identity.CreateAsync(user, cancellationToken));
        EnsureSuccess(await _identity.SetRolesAsync(user, new[] { request.Role }, cancellationToken));

        var token = await _identity.GeneratePasswordResetTokenAsync(user, cancellationToken);
        var link = $"{inviteBaseUrl}?userId={user.Id}&token={Uri.EscapeDataString(token)}";
        await _email.SendAsync(
            user.Email!,
            "Invitation a rejoindre TeamChat",
            $"Bonjour {user.DisplayName}, definissez votre mot de passe : {link}",
            cancellationToken);

        await InvalidateAsync(user.Id, cancellationToken);

        return ToProfile(user, request.Role);
    }

    public async Task<UserProfileDto> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _identity.FindByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Utilisateur introuvable.");

        if (request.DisplayName is not null)
        {
            user.DisplayName = request.DisplayName;
        }

        var deactivated = false;
        if (request.IsActive is { } isActive && isActive != user.IsActive)
        {
            user.IsActive = isActive;
            deactivated = !isActive;
        }

        EnsureSuccess(await _identity.UpdateAsync(user, cancellationToken));

        var currentRoles = await _identity.GetRolesAsync(user, cancellationToken);
        var role = currentRoles.FirstOrDefault() ?? Roles.User;

        if (request.Role is { } newRole && !currentRoles.Contains(newRole))
        {
            EnsureSuccess(await _identity.SetRolesAsync(user, new[] { newRole }, cancellationToken));
            role = newRole;
        }

        if (deactivated)
        {
            await _refreshTokens.RevokeAllForUserAsync(user.Id, cancellationToken);
        }

        await InvalidateAsync(user.Id, cancellationToken);

        return ToProfile(user, role);
    }

    private async Task InvalidateAsync(Guid userId, CancellationToken cancellationToken)
    {
        await _profileCache.InvalidateAsync(userId, cancellationToken);
        await _cache.RemoveByTagAsync(CacheKeys.DirectoryTag(), cancellationToken);
    }

    private static void EnsureSuccess(OperationResult result)
    {
        if (!result.Succeeded)
        {
            throw new ValidationException(result.Errors.Select(e =>
                new FluentValidation.Results.ValidationFailure(string.Empty, e)));
        }
    }

    private static UserProfileDto ToProfile(AppUser user, string role) => new(
        user.Id,
        user.DisplayName,
        user.Email ?? string.Empty,
        user.AvatarUrl,
        role,
        user.IsActive);
}
