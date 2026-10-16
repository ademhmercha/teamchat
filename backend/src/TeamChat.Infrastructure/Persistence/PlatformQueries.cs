using Microsoft.EntityFrameworkCore;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Platform.Dtos;
using TeamChat.Domain.Entities;

namespace TeamChat.Infrastructure.Persistence;

/// <summary>
/// Lecture transverse des societes pour la vue plateforme. Utilise
/// <c>IgnoreQueryFilters()</c> pour compter au-dela des societes : cette
/// requete n'est exposee que par <c>PlatformController</c> (super-admin),
/// protege par [Authorize(Roles = Roles.SuperAdmin)].
/// </summary>
public sealed class PlatformQueries : IPlatformQueries
{
    private readonly TeamChatDbContext _db;

    public PlatformQueries(TeamChatDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PlatformTenantDto>> GetTenantsAsync(CancellationToken cancellationToken = default)
    {
        var tenants = await _db.Tenants.AsNoTracking()
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        if (tenants.Count == 0)
        {
            return Array.Empty<PlatformTenantDto>();
        }

        var counts = await LoadCountsAsync(cancellationToken);
        return tenants.Select(t => ToDto(t, counts)).ToList();
    }

    public async Task<PlatformTenantDto?> GetTenantAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (tenant is null)
        {
            return null;
        }

        var counts = await LoadCountsAsync(cancellationToken);
        return ToDto(tenant, counts);
    }

    private async Task<TenantCounts> LoadCountsAsync(CancellationToken cancellationToken)
    {
        var userRows = await _db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.DeletedAt == null && u.TenantId != null)
            .GroupBy(u => u.TenantId!.Value)
            .Select(g => new { TenantId = g.Key, Total = g.Count(), Active = g.Count(u => u.IsActive) })
            .ToListAsync(cancellationToken);
        var users = userRows.ToDictionary(x => x.TenantId, x => (x.Total, x.Active));

        var groupRows = await _db.Conversations.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.Type == ConversationType.Group && c.ArchivedAt == null)
            .GroupBy(c => c.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var groups = groupRows.ToDictionary(x => x.TenantId, x => x.Count);

        var messageRows = await _db.Messages.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.DeletedAt == null)
            .GroupBy(m => m.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var messages = messageRows.ToDictionary(x => x.TenantId, x => x.Count);

        return new TenantCounts(users, groups, messages);
    }

    private static PlatformTenantDto ToDto(Tenant tenant, TenantCounts counts)
        => new(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.IsActive,
            tenant.CreatedAt,
            counts.UserCount(tenant.Id),
            counts.ActiveUserCount(tenant.Id),
            counts.GroupCount(tenant.Id),
            counts.MessageCount(tenant.Id));

    private sealed record TenantCounts(
        IReadOnlyDictionary<Guid, (int Total, int Active)> Users,
        IReadOnlyDictionary<Guid, int> Groups,
        IReadOnlyDictionary<Guid, int> Messages)
    {
        public int UserCount(Guid id) => Users.GetValueOrDefault(id).Total;

        public int ActiveUserCount(Guid id) => Users.GetValueOrDefault(id).Active;

        public int GroupCount(Guid id) => Groups.GetValueOrDefault(id);

        public int MessageCount(Guid id) => Messages.GetValueOrDefault(id);
    }
}
