using System.Security.Cryptography;
using FluentValidation;
using Microsoft.Extensions.Options;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Auth.Dtos;
using TeamChat.Application.Common;
using TeamChat.Application.Users.Dtos;
using TeamChat.Domain.Entities;

namespace TeamChat.Application.Auth;

public sealed class AuthService : IAuthService
{
    private readonly IIdentityService _identity;
    private readonly IJwtTokenService _jwt;
    private readonly IRefreshTokenStore _refreshTokens;
    private readonly IUserProfileCache _profileCache;
    private readonly IGoogleTokenValidator _google;
    private readonly IEmailSender _email;
    private readonly ITenantService _tenants;
    private readonly JwtOptions _jwtOptions;
    private readonly TimeProvider _clock;

    public AuthService(
        IIdentityService identity,
        IJwtTokenService jwt,
        IRefreshTokenStore refreshTokens,
        IUserProfileCache profileCache,
        IGoogleTokenValidator google,
        IEmailSender email,
        ITenantService tenants,
        IOptions<JwtOptions> jwtOptions,
        TimeProvider clock)
    {
        _identity = identity;
        _jwt = jwt;
        _refreshTokens = refreshTokens;
        _profileCache = profileCache;
        _google = google;
        _email = email;
        _tenants = tenants;
        _jwtOptions = jwtOptions.Value;
        _clock = clock;
    }

    public async Task<AuthResult> RegisterCompanyAsync(
        RegisterCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _identity.FindByEmailAsync(email, cancellationToken) is not null)
        {
            throw new ConflictException("Un compte avec cet email existe deja.");
        }

        var tenant = await _tenants.CreateAsync(request.CompanyName, cancellationToken);

        var owner = new AppUser
        {
            UserName = email,
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            TenantId = tenant.Id,
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = _clock.GetUtcNow(),
        };

        var created = await _identity.CreateAsync(owner, request.Password, cancellationToken);
        if (!created.Succeeded)
        {
            throw new ValidationException(created.Errors.Select(e =>
                new FluentValidation.Results.ValidationFailure(string.Empty, e)));
        }

        var roles = await _identity.SetRolesAsync(owner, new[] { Domain.Constants.Roles.Owner }, cancellationToken);
        if (!roles.Succeeded)
        {
            throw new ValidationException(roles.Errors.Select(e =>
                new FluentValidation.Results.ValidationFailure(string.Empty, e)));
        }

        return await IssueTokensAsync(owner, cancellationToken);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _identity.FindByEmailAsync(request.Email, cancellationToken)
            ?? throw new UnauthorizedException("Email ou mot de passe invalide.");

        if (!user.IsActive)
        {
            throw new UnauthorizedException("Compte desactive.");
        }

        await EnsureTenantActiveAsync(user, cancellationToken);

        var outcome = await _identity.CheckPasswordAsync(user, request.Password, lockoutOnFailure: true, cancellationToken);

        switch (outcome)
        {
            case SignInOutcome.Success:
                break;
            case SignInOutcome.LockedOut:
                throw new UnauthorizedException("Compte temporairement verrouille. Reessayez plus tard.");
            default:
                throw new UnauthorizedException("Email ou mot de passe invalide.");
        }

        user.LastSeenAt = _clock.GetUtcNow();
        await _identity.UpdateAsync(user, cancellationToken);

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthResult> LoginWithGoogleAsync(GoogleLoginRequest request, CancellationToken cancellationToken = default)
    {
        var info = await _google.ValidateAsync(request.IdToken, cancellationToken)
            ?? throw new UnauthorizedException("Jeton Google invalide.");

        var user = await _identity.FindByEmailAsync(info.Email, cancellationToken)
            ?? throw new UnauthorizedException("Aucun compte n'est associe a cet email. Contactez un administrateur.");

        if (!user.IsActive)
        {
            throw new UnauthorizedException("Compte desactive.");
        }

        await EnsureTenantActiveAsync(user, cancellationToken);

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new UnauthorizedException("Refresh token manquant.");
        }

        var hash = Hash(refreshToken);
        var stored = await _refreshTokens.FindByHashAsync(hash, cancellationToken)
            ?? throw new UnauthorizedException("Refresh token invalide.");

        if (stored.RevokedAt is not null)
        {
            // Reutilisation d'un token deja tourne : on revoque toute la famille par precaution.
            await _refreshTokens.RevokeAllForUserAsync(stored.UserId, cancellationToken);
            throw new UnauthorizedException("Refresh token deja utilise.");
        }

        if (!stored.IsActive)
        {
            throw new UnauthorizedException("Refresh token expire.");
        }

        var user = await _identity.FindByIdAsync(stored.UserId, cancellationToken)
            ?? throw new UnauthorizedException("Utilisateur introuvable.");

        if (!user.IsActive)
        {
            throw new UnauthorizedException("Compte desactive.");
        }

        await EnsureTenantActiveAsync(user, cancellationToken);

        var roles = await _identity.GetRolesAsync(user, cancellationToken);
        var access = _jwt.CreateAccessToken(user, roles);

        var newRaw = GenerateRefreshToken();
        var newHash = Hash(newRaw);
        var newExpiresAt = _clock.GetUtcNow().AddDays(_jwtOptions.RefreshTokenDays);

        stored.RevokedAt = _clock.GetUtcNow();
        stored.ReplacedByTokenHash = newHash;
        await _refreshTokens.UpdateAsync(stored, cancellationToken);

        await _refreshTokens.AddAsync(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = newHash,
            ExpiresAt = newExpiresAt,
        }, cancellationToken);

        return new AuthResult(access.Token, access.ExpiresAt, newRaw, newExpiresAt, await ToProfileAsync(user, roles, cancellationToken));
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var stored = await _refreshTokens.FindByHashAsync(Hash(refreshToken), cancellationToken);
        if (stored is { RevokedAt: null })
        {
            stored.RevokedAt = _clock.GetUtcNow();
            await _refreshTokens.UpdateAsync(stored, cancellationToken);
        }
    }

    public async Task<UserProfileDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _identity.FindByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Utilisateur introuvable.");

        var roles = await _identity.GetRolesAsync(user, cancellationToken);
        return await ToProfileAsync(user, roles, cancellationToken);
    }

    public async Task AcceptInviteAsync(AcceptInviteRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _identity.FindByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException("Utilisateur introuvable.");

        var result = await _identity.ResetPasswordAsync(user, request.Token, request.Password, cancellationToken);
        if (!result.Succeeded)
        {
            throw new ValidationException(result.Errors.Select(e =>
                new FluentValidation.Results.ValidationFailure(nameof(request.Password), e)));
        }

        await _profileCache.InvalidateAsync(user.Id, cancellationToken);
    }

    public async Task ForgotPasswordAsync(
        ForgotPasswordRequest request,
        string resetBaseUrl,
        CancellationToken cancellationToken = default)
    {
        var user = await _identity.FindByEmailAsync(request.Email, cancellationToken);

        // Reponse volontairement identique que le compte existe ou non (anti-enumeration).
        if (user is null || user.DeletedAt is not null || !user.IsActive)
        {
            return;
        }

        var token = await _identity.GeneratePasswordResetTokenAsync(user, cancellationToken);
        var link = $"{resetBaseUrl}?userId={user.Id}&token={Uri.EscapeDataString(token)}";
        await _email.SendAsync(
            user.Email!,
            "Reinitialisation de votre mot de passe TeamChat",
            $"Bonjour {user.DisplayName}, definissez un nouveau mot de passe : {link}",
            cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _identity.FindByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException("Utilisateur introuvable.");

        if (user.DeletedAt is not null || !user.IsActive)
        {
            throw new BadRequestException("Compte inactif.");
        }

        var result = await _identity.ResetPasswordAsync(user, request.Token, request.Password, cancellationToken);
        if (!result.Succeeded)
        {
            throw new ValidationException(result.Errors.Select(e =>
                new FluentValidation.Results.ValidationFailure(nameof(request.Password), e)));
        }

        // Un changement de mot de passe invalide toutes les sessions existantes.
        await _refreshTokens.RevokeAllForUserAsync(user.Id, cancellationToken);
        await _profileCache.InvalidateAsync(user.Id, cancellationToken);
    }

    private async Task EnsureTenantActiveAsync(AppUser user, CancellationToken cancellationToken)
    {
        if (user.TenantId is { } tenantId && !await _tenants.IsActiveAsync(tenantId, cancellationToken))
        {
            throw new UnauthorizedException("Societe desactivee.");
        }
    }

    private async Task<AuthResult> IssueTokensAsync(AppUser user, CancellationToken cancellationToken)
    {
        var roles = await _identity.GetRolesAsync(user, cancellationToken);
        var access = _jwt.CreateAccessToken(user, roles);

        var raw = GenerateRefreshToken();
        var refreshExpiresAt = _clock.GetUtcNow().AddDays(_jwtOptions.RefreshTokenDays);
        await _refreshTokens.AddAsync(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = Hash(raw),
            ExpiresAt = refreshExpiresAt,
        }, cancellationToken);

        return new AuthResult(access.Token, access.ExpiresAt, raw, refreshExpiresAt, await ToProfileAsync(user, roles, cancellationToken));
    }

    private async Task<UserProfileDto> ToProfileAsync(
        AppUser user,
        IReadOnlyList<string> roles,
        CancellationToken cancellationToken)
    {
        string? tenantName = null;
        if (user.TenantId is { } tenantId)
        {
            var tenant = await _tenants.GetByIdAsync(tenantId, cancellationToken);
            tenantName = tenant?.Name;
        }

        return new UserProfileDto(
            user.Id,
            user.DisplayName,
            user.Email ?? string.Empty,
            user.AvatarUrl,
            roles.FirstOrDefault() ?? string.Empty,
            user.IsActive,
            user.TenantId,
            tenantName);
    }

    internal static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    internal static string Hash(string token)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
