using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamChat.Api.Infrastructure;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Company.Dtos;
using TeamChat.Application.Tenants.Dtos;
using TeamChat.Domain.Constants;

namespace TeamChat.Api.Controllers;

/// <summary>
/// Administration de la societe de l'utilisateur courant. Seul l'Owner peut
/// la renommer (les Admin gerent uniquement les membres).
/// </summary>
[ApiController]
[Authorize(Roles = Roles.Owner)]
[Route("api/company")]
public sealed class CompanyController : ControllerBase
{
    private readonly ICompanyService _company;

    public CompanyController(ICompanyService company)
    {
        _company = company;
    }

    [HttpPatch]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TenantDto>> Rename(
        [FromBody] RenameCompanyRequest request,
        CancellationToken cancellationToken)
        => Ok(await _company.RenameAsync(User.GetUserId(), request.Name, cancellationToken));
}
