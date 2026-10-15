using Microsoft.EntityFrameworkCore;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Tenants.Dtos;
using TeamChat.Domain.Entities;
using TeamChat.Infrastructure.Persistence;

namespace TeamChat.Infrastructure.Tenants;

/// <summary>Creation et lecture des societes (table <c>Tenants</c>, non filtree).</summary>
public sealed class TenantService : ITenantService
{
    private readonly TeamChatDbContext _db;
    private readonly TimeProvider _clock;

    public TenantService(TeamChatDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<TenantDto> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var cleanName = (name ?? string.Empty).Trim();
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = cleanName,
            Slug = BuildSlug(cleanName),
            IsActive = true,
            CreatedAt = _clock.GetUtcNow(),
        };

        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(tenant);
    }

    public async Task<TenantDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        return tenant is null ? null : ToDto(tenant);
    }

    private static string BuildSlug(string name)
    {
        var chars = name.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        var slug = new string(chars).Trim('-');
        while (slug.Contains("--"))
        {
            slug = slug.Replace("--", "-");
        }

        if (slug.Length == 0)
        {
            slug = "societe";
        }

        if (slug.Length > 100)
        {
            slug = slug[..100];
        }

        // Suffixe aleatoire : garantit l'unicite du slug meme pour des noms identiques.
        return $"{slug}-{Guid.NewGuid().ToString("N")[..6]}";
    }

    private static TenantDto ToDto(Tenant tenant) =>
        new(tenant.Id, tenant.Name, tenant.Slug, tenant.IsActive, tenant.CreatedAt);
}
