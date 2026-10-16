using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Platform.Dtos;
using TeamChat.Domain.Constants;

namespace TeamChat.Api.Controllers;

/// <summary>
/// Administration plateforme des societes (super-admin uniquement). Ne renvoie
/// jamais de donnees metier d'une societe : uniquement les societes et leurs
/// compteurs agreges.
/// </summary>
[ApiController]
[Authorize(Roles = Roles.SuperAdmin)]
[Route("api/platform/tenants")]
public sealed class PlatformController : ControllerBase
{
    private readonly IPlatformService _platform;

    public PlatformController(IPlatformService platform)
    {
        _platform = platform;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PlatformTenantDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<PlatformTenantDto>> List(CancellationToken cancellationToken)
        => _platform.GetTenantsAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PlatformTenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlatformTenantDto>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await _platform.GetTenantAsync(id, cancellationToken));

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(PlatformTenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlatformTenantDto>> Update(
        Guid id,
        [FromBody] UpdateTenantRequest request,
        CancellationToken cancellationToken)
        => Ok(await _platform.UpdateTenantAsync(id, request, cancellationToken));
}
