using Microsoft.AspNetCore.Identity;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Common;
using TeamChat.Domain.Entities;

namespace TeamChat.Infrastructure.Identity;

public sealed class IdentityService : IIdentityService
{
    private readonly UserManager<AppUser> _userManager;

    public IdentityService(UserManager<AppUser> userManager)
    {
        _userManager = userManager;
    }

    public Task<AppUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
        => _userManager.FindByEmailAsync(email);

    public Task<AppUser?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _userManager.FindByIdAsync(id.ToString());

    public async Task<SignInOutcome> CheckPasswordAsync(
        AppUser user,
        string password,
        bool lockoutOnFailure,
        CancellationToken cancellationToken = default)
    {
        if (_userManager.SupportsUserLockout && await _userManager.IsLockedOutAsync(user))
        {
            return SignInOutcome.LockedOut;
        }

        if (!await _userManager.CheckPasswordAsync(user, password))
        {
            if (lockoutOnFailure && _userManager.SupportsUserLockout)
            {
                await _userManager.AccessFailedAsync(user);
            }

            return SignInOutcome.InvalidCredentials;
        }

        if (_userManager.SupportsUserLockout)
        {
            await _userManager.ResetAccessFailedCountAsync(user);
        }

        return SignInOutcome.Success;
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(AppUser user, CancellationToken cancellationToken = default)
        => (await _userManager.GetRolesAsync(user)).ToList();

    public async Task<OperationResult> CreateAsync(AppUser user, CancellationToken cancellationToken = default)
        => Map(await _userManager.CreateAsync(user));

    public async Task<OperationResult> UpdateAsync(AppUser user, CancellationToken cancellationToken = default)
        => Map(await _userManager.UpdateAsync(user));

    public async Task<OperationResult> SetRolesAsync(
        AppUser user,
        IEnumerable<string> roles,
        CancellationToken cancellationToken = default)
    {
        var current = await _userManager.GetRolesAsync(user);
        var remove = await _userManager.RemoveFromRolesAsync(user, current);
        if (!remove.Succeeded)
        {
            return Map(remove);
        }

        return Map(await _userManager.AddToRolesAsync(user, roles));
    }

    public Task<string> GeneratePasswordResetTokenAsync(AppUser user, CancellationToken cancellationToken = default)
        => _userManager.GeneratePasswordResetTokenAsync(user);

    public async Task<OperationResult> ResetPasswordAsync(
        AppUser user,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default)
        => Map(await _userManager.ResetPasswordAsync(user, token, newPassword));

    public async Task<OperationResult> ChangePasswordAsync(
        AppUser user,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
        => Map(await _userManager.ChangePasswordAsync(user, currentPassword, newPassword));

    private static OperationResult Map(IdentityResult result)
        => result.Succeeded
            ? OperationResult.Ok()
            : OperationResult.Fail(result.Errors.Select(e => e.Description).ToArray());
}
