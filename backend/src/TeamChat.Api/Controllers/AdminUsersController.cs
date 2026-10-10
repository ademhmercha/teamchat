using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Users.Dtos;
using TeamChat.Domain.Constants;

namespace TeamChat.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/admin/users")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _admin;

    public AdminUsersController(IAdminUserService admin)
    {
        _admin = admin;
    }

    [HttpPost]
    public async Task<ActionResult<UserProfileDto>> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var inviteBaseUrl = $"{Request.Scheme}://{Request.Host}/accept-invite";
        var profile = await _admin.CreateAsync(request, inviteBaseUrl, cancellationToken);
        return CreatedAtAction(nameof(UsersController.Get), "Users", new { id = profile.Id }, profile);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<UserProfileDto>> Update(
        Guid id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
        => Ok(await _admin.UpdateAsync(id, request, cancellationToken));
}
