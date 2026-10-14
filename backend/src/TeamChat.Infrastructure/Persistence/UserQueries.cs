using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Caching;
using TeamChat.Application.Common;
using TeamChat.Application.Users.Dtos;
using TeamChat.Domain.Constants;

namespace TeamChat.Infrastructure.Persistence;

/// <summary>Lecture de l'annuaire avec mise en cache (60 s) des resultats de recherche.</summary>
public sealed class UserQueries : IUserQueries
{
    private static readonly TimeSpan DirectoryTtl = TimeSpan.FromSeconds(60);

    private readonly TeamChatDbContext _db;
    private readonly ICacheService _cache;

    public UserQueries(TeamChatDbContext db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public Task<PagedResult<UserListItemDto>> SearchAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
        => _cache.GetOrCreateAsync(
            CacheKeys.Directory(page, pageSize, search),
            token => QueryAsync(search, page, pageSize, token),
            DirectoryTtl,
            new[] { CacheKeys.DirectoryTag() },
            cancellationToken).AsTask();

    private async ValueTask<PagedResult<UserListItemDto>> QueryAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = _db.Users.AsNoTracking().Where(u => u.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u =>
                u.DisplayName.ToLower().Contains(term) ||
                (u.Email != null && u.Email.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(u => u.DisplayName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserListItemDto(u.Id, u.DisplayName, u.Email ?? string.Empty, u.AvatarUrl, u.IsActive))
            .ToListAsync(cancellationToken);

        return new PagedResult<UserListItemDto>(items, page, pageSize, total);
    }

    public async Task<UserProfileDto?> GetProfileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id && u.DeletedAt == null, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var role = await (from userRole in _db.UserRoles
                          join r in _db.Roles on userRole.RoleId equals r.Id
                          where userRole.UserId == id
                          select r.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        return new UserProfileDto(user.Id, user.DisplayName, user.Email ?? string.Empty, user.AvatarUrl, role, user.IsActive);
    }

    public async Task<PagedResult<UserProfileDto>> AdminSearchAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Users.AsNoTracking().Where(u => u.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u =>
                u.DisplayName.ToLower().Contains(term) ||
                (u.Email != null && u.Email.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderBy(u => u.DisplayName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new { u.Id, u.DisplayName, u.Email, u.AvatarUrl, u.IsActive })
            .ToListAsync(cancellationToken);

        var ids = users.Select(u => u.Id).ToList();
        var rolePairs = await (from userRole in _db.UserRoles
                               join r in _db.Roles on userRole.RoleId equals r.Id
                               where ids.Contains(userRole.UserId)
                               select new { userRole.UserId, r.Name })
            .ToListAsync(cancellationToken);
        var roles = rolePairs
            .GroupBy(pair => pair.UserId)
            .ToDictionary(group => group.Key, group => group.First().Name);

        var items = users
            .Select(u => new UserProfileDto(
                u.Id,
                u.DisplayName,
                u.Email ?? string.Empty,
                u.AvatarUrl,
                roles.GetValueOrDefault(u.Id, Roles.User),
                u.IsActive))
            .ToList();

        return new PagedResult<UserProfileDto>(items, page, pageSize, total);
    }

    public Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken = default)
        => (from user in _db.Users.AsNoTracking()
            join userRole in _db.UserRoles on user.Id equals userRole.UserId
            join role in _db.Roles on userRole.RoleId equals role.Id
            where role.Name == Roles.Admin && user.IsActive && user.DeletedAt == null
            select user.Id).Distinct().CountAsync(cancellationToken);
}
