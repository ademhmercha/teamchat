using TeamChat.Application.Common;
using TeamChat.Domain.Entities;

namespace TeamChat.Application.Abstractions;

public enum SignInOutcome
{
    Success,
    InvalidCredentials,
    LockedOut,
    NotAllowed,
}

/// <summary>
/// Abstraction des operations ASP.NET Core Identity utilisees par l'Application.
/// L'implementation vit dans Infrastructure (UserManager / SignInManager).
/// </summary>
public interface IIdentityService
{
    Task<AppUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<AppUser?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SignInOutcome> CheckPasswordAsync(
        AppUser user,
        string password,
        bool lockoutOnFailure,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetRolesAsync(AppUser user, CancellationToken cancellationToken = default);

    Task<OperationResult> CreateAsync(AppUser user, CancellationToken cancellationToken = default);

    Task<OperationResult> UpdateAsync(AppUser user, CancellationToken cancellationToken = default);

    Task<OperationResult> SetRolesAsync(
        AppUser user,
        IEnumerable<string> roles,
        CancellationToken cancellationToken = default);

    Task<string> GeneratePasswordResetTokenAsync(AppUser user, CancellationToken cancellationToken = default);

    Task<OperationResult> ResetPasswordAsync(
        AppUser user,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default);
}
