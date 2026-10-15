using TeamChat.Application.Tenants.Dtos;

namespace TeamChat.Application.Abstractions;

/// <summary>Gestion des societes (creation, lecture).</summary>
public interface ITenantService
{
    Task<TenantDto> CreateAsync(string name, CancellationToken cancellationToken = default);

    Task<TenantDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
