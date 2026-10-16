using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamChat.Api.Infrastructure;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Common;
using TeamChat.Application.Profile.Dtos;
using TeamChat.Application.Users.Dtos;

namespace TeamChat.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserDirectoryService _directory;
    private readonly IProfileService _profile;

    public UsersController(IUserDirectoryService directory, IProfileService profile)
    {
        _directory = directory;
        _profile = profile;
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

    [HttpPatch("me")]
    public async Task<ActionResult<UserProfileDto>> UpdateMe(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
        => Ok(await _profile.UpdateAsync(User.GetUserId(), request, cancellationToken));

    [HttpPost("me/avatar")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserProfileDto>> UploadAvatar(
        [FromForm] AvatarUploadForm form,
        CancellationToken cancellationToken)
    {
        if (form.File is null || form.File.Length == 0)
        {
            return BadRequest(new ProblemDetails { Title = "Fichier manquant.", Detail = "Aucun fichier n'a ete fourni." });
        }

        await using var stream = form.File.OpenReadStream();
        var upload = new AvatarUpload(form.File.FileName, form.File.ContentType, form.File.Length, stream);
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        return Ok(await _profile.UploadAvatarAsync(User.GetUserId(), upload, baseUrl, cancellationToken));
    }

    [HttpDelete("me/avatar")]
    public async Task<IActionResult> RemoveAvatar(CancellationToken cancellationToken)
    {
        await _profile.RemoveAvatarAsync(User.GetUserId(), cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/avatar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Avatar(Guid id, CancellationToken cancellationToken)
    {
        var avatar = await _profile.OpenAvatarAsync(id, cancellationToken);
        Response.Headers.CacheControl = "private, no-cache";
        return new FileStreamResult(avatar.Content, avatar.ContentType);
    }
}

public sealed class AvatarUploadForm
{
    public IFormFile? File { get; set; }
}
