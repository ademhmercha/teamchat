using TeamChat.Application.Abstractions;
using TeamChat.Application.Common;
using TeamChat.Application.Tenants.Dtos;

namespace TeamChat.Application.Company;

/// <summary>
/// Operations d'administration de la societe de l'utilisateur courant.
/// Reservees au role Owner par le controleur (voir <c>CompanyController</c>).
/// </summary>
public sealed class CompanyService : ICompanyService
{
    private const int MaxNameLength = 100;

    private readonly IIdentityService _identity;
    private readonly ITenantService _tenants;

    public CompanyService(IIdentityService identity, ITenantService tenants)
    {
        _identity = identity;
        _tenants = tenants;
    }

    public async Task<TenantDto> RenameAsync(Guid userId, string name, CancellationToken cancellationToken = default)
    {
        var user = await _identity.FindByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Utilisateur introuvable.");

        if (user.TenantId is not { } tenantId)
        {
            throw new BadRequestException("Ce compte n'est rattache a aucune societe.");
        }

        var clean = name.Trim();
        if (clean.Length is 0 or > MaxNameLength)
        {
            throw new BadRequestException("Le nom de la societe est invalide.");
        }

        await _tenants.UpdateAsync(tenantId, clean, isActive: null, cancellationToken);

        return await _tenants.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new NotFoundException("Societe introuvable.");
    }
}
