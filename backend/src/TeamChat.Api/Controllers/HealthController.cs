using Microsoft.AspNetCore.Mvc;
using TeamChat.Application.Abstractions;

namespace TeamChat.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    private const string HealthCacheKey = "tc:v1:health";

    private readonly ICacheService _cache;

    public HealthController(ICacheService cache)
    {
        _cache = cache;
    }

    /// <summary>
    /// Verifie que l'API repond et que le cache (HybridCache) est utilisable.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var status = await _cache.GetOrCreateAsync(
            HealthCacheKey,
            _ => ValueTask.FromResult("ok"),
            TimeSpan.FromSeconds(5),
            cancellationToken: cancellationToken);

        return Ok(new
        {
            status,
            service = "TeamChat.Api",
            cache = _cache.GetType().Name,
        });
    }
}
