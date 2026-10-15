using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Admin.Dtos;
using TeamChat.Domain.Constants;

namespace TeamChat.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.CompanyAdmins)]
[Route("api/admin/stats")]
public sealed class AdminStatsController : ControllerBase
{
    private readonly IAdminStatsService _stats;

    public AdminStatsController(IAdminStatsService stats)
    {
        _stats = stats;
    }

    [HttpGet]
    public Task<AdminStatsDto> GetStats(CancellationToken cancellationToken)
        => _stats.GetStatsAsync(cancellationToken);
}
