using TeamChat.Application.Tenants.Dtos;

namespace TeamChat.Application.Abstractions;

public interface ITenantService
{
    Task<TenantDto> CreateAsync(string name, CancellationToken cancellationToken = default);

    Task<TenantDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetActiveIdsAsync(CancellationToken cancellationToken = default);

    Task<bool> IsActiveAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Guid id,
        string? name,
        bool? isActive,
        CancellationToken cancellationToken = default);
}
