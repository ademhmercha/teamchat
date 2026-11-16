using TeamChat.Application.Tenants.Dtos;

namespace TeamChat.Application.Abstractions;

public interface ICompanyService
{
    Task<TenantDto> RenameAsync(Guid userId, string name, CancellationToken cancellationToken = default);
}
