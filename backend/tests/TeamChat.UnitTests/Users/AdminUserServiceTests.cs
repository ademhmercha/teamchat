using TeamChat.Application.Abstractions;
using TeamChat.Application.Common;
using TeamChat.Application.Users;
using TeamChat.Application.Users.Dtos;
using TeamChat.Domain.Constants;
using TeamChat.Domain.Entities;

namespace TeamChat.UnitTests.Users;

/// <summary>
/// Regles metier de la gestion des comptes par un administrateur :
/// protection du dernier administrateur actif et suppression logique (anonymisation).
/// </summary>
public sealed class AdminUserServiceTests
{
    [Fact]
    public async Task Update_deactivating_last_active_admin_is_rejected()
    {
        var user = ActiveAdmin();
        var service = Build(user, activeAdmins: 1, roles: Roles.Admin);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.UpdateAsync(user.Id, new UpdateUserRequest(null, null, false)));
    }

    [Fact]
    public async Task Update_demoting_last_active_admin_is_rejected()
    {
        var user = ActiveAdmin();
        var service = Build(user, activeAdmins: 1, roles: Roles.Admin);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.UpdateAsync(user.Id, new UpdateUserRequest(null, Roles.User, null)));
    }

    [Fact]
    public async Task Update_deactivating_admin_with_other_admins_succeeds()
    {
        var user = ActiveAdmin();
        Build(user, activeAdmins: 2, roles: Roles.Admin, out var service);

        var profile = await service.UpdateAsync(user.Id, new UpdateUserRequest(null, null, false));

        Assert.False(profile.IsActive);
        Assert.False(user.IsActive);
    }

    [Fact]
    public async Task Delete_last_active_admin_is_rejected()
    {
        var user = ActiveAdmin();
        var service = Build(user, activeAdmins: 1, roles: Roles.Admin);

        await Assert.ThrowsAsync<BadRequestException>(() => service.DeleteAsync(user.Id));
    }

    [Fact]
    public async Task Delete_anonymizes_and_deactivates_account()
    {
        var user = ActiveAdmin();
        user.Email = "cible@teamchat.test";
        user.UserName = "cible@teamchat.test";
        user.AvatarUrl = "https://cdn/avatar.png";
        var refreshTokens = new FakeRefreshTokenStore();
        var sessions = new FakeSessionTerminator();
        var identity = Build(user, activeAdmins: 3, roles: Roles.Admin, refreshTokens, sessions, out var service);

        await service.DeleteAsync(user.Id);

        Assert.False(user.IsActive);
        Assert.NotNull(user.DeletedAt);
        Assert.Equal(AdminUserService.DeletedDisplayName, user.DisplayName);
        Assert.Null(user.AvatarUrl);
        Assert.StartsWith("supprime+", user.Email);
        Assert.Equal(user.Email, user.UserName);
        Assert.Contains(user.Id, refreshTokens.Revoked);
        Assert.Contains(user.Id, sessions.Terminated);
        Assert.Empty(await identity.GetRolesAsync(user));
    }

    [Fact]
    public async Task Update_deleted_user_is_not_found()
    {
        var user = ActiveAdmin();
        user.DeletedAt = DateTimeOffset.UtcNow;
        var service = Build(user, activeAdmins: 1, roles: Roles.Admin);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.UpdateAsync(user.Id, new UpdateUserRequest("Nouveau", null, null)));
    }

    private static AppUser ActiveAdmin() => new()
    {
        Id = Guid.NewGuid(),
        DisplayName = "Admin Test",
        Email = "admin@teamchat.test",
        UserName = "admin@teamchat.test",
        IsActive = true,
    };

    private static AdminUserService Build(
        AppUser user,
        int activeAdmins,
        string roles)
    {
        Build(user, activeAdmins, roles, new FakeRefreshTokenStore(), new FakeSessionTerminator(), out var service);
        return service;
    }

    private static FakeIdentityService Build(
        AppUser user,
        int activeAdmins,
        string roles,
        out AdminUserService service)
        => Build(user, activeAdmins, roles, new FakeRefreshTokenStore(), new FakeSessionTerminator(), out service);

    private static FakeIdentityService Build(
        AppUser user,
        int activeAdmins,
        string roles,
        FakeRefreshTokenStore refreshTokens,
        FakeSessionTerminator sessions,
        out AdminUserService service)
    {
        var identity = new FakeIdentityService(user, roles);
        service = new AdminUserService(
            identity,
            refreshTokens,
            new FakeUserProfileCache(),
            new FakeUserQueries(activeAdmins),
            new FakeMembershipService(),
            sessions,
            new FakeCacheService(),
            new FakeEmailSender());
        return identity;
    }

    private sealed class FakeIdentityService : IIdentityService
    {
        private readonly AppUser _user;
        private readonly List<string> _roles;

        public FakeIdentityService(AppUser user, params string[] roles)
        {
            _user = user;
            _roles = roles.ToList();
        }

        public Task<AppUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
            => Task.FromResult<AppUser?>(_user.Email == email ? _user : null);

        public Task<AppUser?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<AppUser?>(_user.Id == id ? _user : null);

        public Task<SignInOutcome> CheckPasswordAsync(AppUser user, string password, bool lockoutOnFailure, CancellationToken cancellationToken = default)
            => Task.FromResult(SignInOutcome.Success);

        public Task<IReadOnlyList<string>> GetRolesAsync(AppUser user, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(_roles.ToList());

        public Task<OperationResult> CreateAsync(AppUser user, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult.Ok());

        public Task<OperationResult> UpdateAsync(AppUser user, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult.Ok());

        public Task<OperationResult> SetRolesAsync(AppUser user, IEnumerable<string> roles, CancellationToken cancellationToken = default)
        {
            _roles.Clear();
            _roles.AddRange(roles);
            return Task.FromResult(OperationResult.Ok());
        }

        public Task<string> GeneratePasswordResetTokenAsync(AppUser user, CancellationToken cancellationToken = default)
            => Task.FromResult("reset-token");

        public Task<OperationResult> ResetPasswordAsync(AppUser user, string token, string newPassword, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult.Ok());

        public Task<OperationResult> ChangePasswordAsync(AppUser user, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult.Ok());
    }

    private sealed class FakeUserQueries : IUserQueries
    {
        private readonly int _activeAdmins;

        public FakeUserQueries(int activeAdmins) => _activeAdmins = activeAdmins;

        public Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_activeAdmins);

        public Task<PagedResult<UserListItemDto>> SearchAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<UserProfileDto?> GetProfileAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PagedResult<UserProfileDto>> AdminSearchAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeRefreshTokenStore : IRefreshTokenStore
    {
        public List<Guid> Revoked { get; } = new();

        public Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
            => Task.FromResult<RefreshToken?>(null);

        public Task UpdateAsync(RefreshToken token, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            Revoked.Add(userId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUserProfileCache : IUserProfileCache
    {
        public Task<UserAccessProfile?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<UserAccessProfile?>(null);

        public ValueTask InvalidateAsync(Guid userId, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }

    private sealed class FakeMembershipService : IMembershipService
    {
        public Task<bool> IsMemberAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task InvalidateConversationAsync(Guid conversationId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task InvalidateUserAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeSessionTerminator : IUserSessionTerminator
    {
        public List<Guid> Terminated { get; } = new();

        public Task TerminateAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            Terminated.Add(userId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCacheService : ICacheService
    {
        public ValueTask<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, TimeSpan? ttl = null, IEnumerable<string>? tags = null, CancellationToken cancellationToken = default)
            => factory(cancellationToken);

        public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<(string To, string Subject)> Sent { get; } = new();

        public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        {
            Sent.Add((to, subject));
            return Task.CompletedTask;
        }
    }
}
