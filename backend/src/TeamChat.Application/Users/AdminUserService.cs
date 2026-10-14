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
    /// <summary>Nom d'affichage des comptes supprimes (les messages sont conserves).</summary>
    public const string DeletedDisplayName = "Utilisateur supprime";

    private readonly IIdentityService _identity;
    private readonly IRefreshTokenStore _refreshTokens;
    private readonly IUserProfileCache _profileCache;
    private readonly IUserQueries _queries;
    private readonly IMembershipService _membership;
    private readonly IUserSessionTerminator _sessions;
    private readonly ICacheService _cache;
    private readonly IEmailSender _email;

    public AdminUserService(
        IIdentityService identity,
        IRefreshTokenStore refreshTokens,
        IUserProfileCache profileCache,
        IUserQueries queries,
        IMembershipService membership,
        IUserSessionTerminator sessions,
        ICacheService cache,
        IEmailSender email)
    {
        _identity = identity;
        _refreshTokens = refreshTokens;
        _profileCache = profileCache;
        _queries = queries;
        _membership = membership;
        _sessions = sessions;
        _cache = cache;
        _email = email;
    }

    public Task<PagedResult<UserProfileDto>> ListAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
        => _queries.AdminSearchAsync(search, page, pageSize, cancellationToken);

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
        var user = await _identity.FindByIdAsync(id, cancellationToken);
        if (user is null || user.DeletedAt is not null)
        {
            throw new NotFoundException("Utilisateur introuvable.");
        }

        var currentRoles = await _identity.GetRolesAsync(user, cancellationToken);
        var isActiveAdmin = user.IsActive && currentRoles.Contains(Roles.Admin);
        var willDeactivate = request.IsActive is false && user.IsActive;
        var willDemote = request.Role is { } requestedRole
            && requestedRole != Roles.Admin
            && currentRoles.Contains(Roles.Admin);

        if (isActiveAdmin && (willDeactivate || willDemote))
        {
            await EnsureNotLastAdminAsync(cancellationToken);
        }

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

        var role = currentRoles.FirstOrDefault() ?? Roles.User;

        if (request.Role is { } newRole && !currentRoles.Contains(newRole))
        {
            EnsureSuccess(await _identity.SetRolesAsync(user, new[] { newRole }, cancellationToken));
            role = newRole;
        }

        if (deactivated)
        {
            await _refreshTokens.RevokeAllForUserAsync(user.Id, cancellationToken);
            await _membership.InvalidateUserAsync(user.Id, cancellationToken);
            await _sessions.TerminateAsync(user.Id, cancellationToken);
        }

        await InvalidateAsync(user.Id, cancellationToken);

        return ToProfile(user, role);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _identity.FindByIdAsync(id, cancellationToken);
        if (user is null || user.DeletedAt is not null)
        {
            throw new NotFoundException("Utilisateur introuvable.");
        }

        var roles = await _identity.GetRolesAsync(user, cancellationToken);
        if (user.IsActive && roles.Contains(Roles.Admin))
        {
            await EnsureNotLastAdminAsync(cancellationToken);
        }

        // Suppression logique : on anonymise le profil pour conserver l'historique
        // des messages (l'auteur devient « Utilisateur supprime »).
        user.IsActive = false;
        user.DeletedAt = DateTimeOffset.UtcNow;
        user.DisplayName = DeletedDisplayName;
        user.AvatarUrl = null;
        user.Email = $"supprime+{user.Id:N}@deleted.local";
        user.UserName = user.Email;

        EnsureSuccess(await _identity.UpdateAsync(user, cancellationToken));

        if (roles.Count > 0)
        {
            EnsureSuccess(await _identity.SetRolesAsync(user, Array.Empty<string>(), cancellationToken));
        }

        await _refreshTokens.RevokeAllForUserAsync(user.Id, cancellationToken);
        await _membership.InvalidateUserAsync(user.Id, cancellationToken);
        await _sessions.TerminateAsync(user.Id, cancellationToken);
        await InvalidateAsync(user.Id, cancellationToken);
    }

    public async Task ResetPasswordAsync(
        Guid id,
        string inviteBaseUrl,
        CancellationToken cancellationToken = default)
    {
        var user = await _identity.FindByIdAsync(id, cancellationToken);
        if (user is null || user.DeletedAt is not null)
        {
            throw new NotFoundException("Utilisateur introuvable.");
        }

        var token = await _identity.GeneratePasswordResetTokenAsync(user, cancellationToken);
        var link = $"{inviteBaseUrl}?userId={user.Id}&token={Uri.EscapeDataString(token)}";
        await _email.SendAsync(
            user.Email!,
            "Reinitialisation de votre mot de passe TeamChat",
            $"Bonjour {user.DisplayName}, definissez un nouveau mot de passe : {link}",
            cancellationToken);
    }

    /// <summary>Interdit toute action qui supprimerait le dernier administrateur actif.</summary>
    private async Task EnsureNotLastAdminAsync(CancellationToken cancellationToken)
    {
        var activeAdmins = await _queries.CountActiveAdminsAsync(cancellationToken);
        if (activeAdmins <= 1)
        {
            throw new BadRequestException(
                "Action impossible : ce compte est le dernier administrateur actif.");
        }
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
