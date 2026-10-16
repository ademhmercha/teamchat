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
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _tenant;

    public AdminUserService(
        IIdentityService identity,
        IRefreshTokenStore refreshTokens,
        IUserProfileCache profileCache,
        IUserQueries queries,
        IMembershipService membership,
        IUserSessionTerminator sessions,
        ICacheService cache,
        ICurrentUser currentUser,
        ICurrentTenant tenant)
    {
        _identity = identity;
        _refreshTokens = refreshTokens;
        _profileCache = profileCache;
        _queries = queries;
        _membership = membership;
        _sessions = sessions;
        _cache = cache;
        _currentUser = currentUser;
        _tenant = tenant;
    }

    public Task<PagedResult<UserProfileDto>> ListAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
        => _queries.AdminSearchAsync(search, page, pageSize, cancellationToken);

    public async Task<UserProfileDto> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Roles.TenantRoles.Contains(request.Role))
        {
            throw new ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure(nameof(request.Role), "Role invalide."),
            });
        }

        // Seul le proprietaire (Owner) peut nommer des administrateurs/proprietaires.
        if (request.Role != Roles.User && !_currentUser.IsInRole(Roles.Owner))
        {
            throw new ForbiddenException("Seul le proprietaire peut creer un administrateur.");
        }

        var tenantId = _tenant.TenantId
            ?? throw new ForbiddenException("Vous n'etes rattache a aucune societe.");

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _identity.FindByEmailAsync(email, cancellationToken) is not null)
        {
            throw new ConflictException("Un utilisateur avec cet email existe deja.");
        }

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            TenantId = tenantId,
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        EnsureSuccess(await _identity.CreateAsync(user, request.Password, cancellationToken));
        EnsureSuccess(await _identity.SetRolesAsync(user, new[] { request.Role }, cancellationToken));

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
        var currentRole = currentRoles.FirstOrDefault() ?? Roles.User;
        var callerIsOwner = _currentUser.IsInRole(Roles.Owner);
        var currentIsManager = currentRole is Roles.Owner or Roles.Admin;

        // Un administrateur ne peut pas toucher au proprietaire de la societe.
        if (!callerIsOwner && currentRole == Roles.Owner)
        {
            throw new ForbiddenException("Vous ne pouvez pas modifier le proprietaire de la societe.");
        }

        if (request.Role is { } requestedRole)
        {
            if (!Roles.TenantRoles.Contains(requestedRole))
            {
                throw new ValidationException(new[]
                {
                    new FluentValidation.Results.ValidationFailure(nameof(request.Role), "Role invalide."),
                });
            }

            if (!callerIsOwner && requestedRole != Roles.User)
            {
                throw new ForbiddenException("Seul le proprietaire peut nommer un administrateur.");
            }
        }

        var isActiveManager = user.IsActive && currentIsManager;
        var willDeactivate = request.IsActive is false && user.IsActive;
        var willDemote = request.Role is { } target
            && target != currentRole
            && currentIsManager
            && target == Roles.User;

        if (isActiveManager && (willDeactivate || willDemote))
        {
            await EnsureNotLastAdminAsync(cancellationToken);
        }

        if (request.DisplayName is not null)
        {
            user.DisplayName = request.DisplayName.Trim();
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
        var currentRole = roles.FirstOrDefault() ?? Roles.User;

        if (currentRole == Roles.Owner && !_currentUser.IsInRole(Roles.Owner))
        {
            throw new ForbiddenException("Vous ne pouvez pas supprimer le proprietaire de la societe.");
        }

        if (user.IsActive && currentRole is Roles.Owner or Roles.Admin)
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
        ResetUserPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _identity.FindByIdAsync(id, cancellationToken);
        if (user is null || user.DeletedAt is not null)
        {
            throw new NotFoundException("Utilisateur introuvable.");
        }

        var token = await _identity.GeneratePasswordResetTokenAsync(user, cancellationToken);
        EnsureSuccess(await _identity.ResetPasswordAsync(user, token, request.Password, cancellationToken));
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
        await _cache.RemoveByTagAsync(CacheKeys.DirectoryTag(_tenant.TenantId), cancellationToken);
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
        user.IsActive,
        user.TenantId);
}
