using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamChat.Api.Infrastructure;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Calls.Dtos;

namespace TeamChat.Api.Controllers;

[ApiController]
[Authorize]
public sealed class CallsController : ControllerBase
{
    private readonly ICallService _calls;

    public CallsController(ICallService calls)
    {
        _calls = calls;
    }

    /// <summary>Serveurs ICE (STUN/TURN) pour la connexion WebRTC de l'utilisateur courant.</summary>
    [HttpGet("api/calls/ice-servers")]
    [ProducesResponseType(typeof(IReadOnlyList<IceServerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<IceServerDto>>> GetIceServers(CancellationToken cancellationToken)
        => Ok(await _calls.GetIceServersAsync(User.GetUserId(), cancellationToken));

    /// <summary>Historique des appels de l'utilisateur courant (recents d'abord).</summary>
    [HttpGet("api/calls/history")]
    [ProducesResponseType(typeof(IReadOnlyList<CallHistoryItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CallHistoryItemDto>>> GetHistory(
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
        => Ok(await _calls.GetHistoryAsync(User.GetUserId(), limit ?? 50, cancellationToken));
}
