using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Common;
using TeamChat.Application.Users.Dtos;

namespace TeamChat.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserDirectoryService _directory;

    public UsersController(IUserDirectoryService directory)
    {
        _directory = directory;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<UserListItemDto>>> Search(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        return Ok(await _directory.SearchAsync(search, safePage, safePageSize, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserProfileDto>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await _directory.GetAsync(id, cancellationToken));
}
