using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TeamChat.Api.Contracts;
using TeamChat.Api.Infrastructure;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Auth.Dtos;
using TeamChat.Application.Users.Dtos;

namespace TeamChat.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private const string RefreshCookieName = "tc_refresh";
    private const string RefreshCookiePath = "/api/auth";

    private readonly IAuthService _auth;
    private readonly IWebHostEnvironment _environment;

    public AuthController(IAuthService auth, IWebHostEnvironment environment)
    {
        _auth = auth;
        _environment = environment;
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _auth.LoginAsync(request, cancellationToken);
        return Ok(ToResponse(result));
    }

    [HttpPost("google")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<AuthResponse>> Google(
        [FromBody] GoogleLoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _auth.LoginWithGoogleAsync(request, cancellationToken);
        return Ok(ToResponse(result));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshCookieName] ?? string.Empty;
        var result = await _auth.RefreshAsync(refreshToken, cancellationToken);
        return Ok(ToResponse(result));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await _auth.LogoutAsync(Request.Cookies[RefreshCookieName] ?? string.Empty, cancellationToken);
        DeleteRefreshCookie();
        return NoContent();
    }

    [HttpPost("accept-invite")]
    public async Task<IActionResult> AcceptInvite(
        [FromBody] AcceptInviteRequest request,
        CancellationToken cancellationToken)
    {
        await _auth.AcceptInviteAsync(request, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserProfileDto>> Me(CancellationToken cancellationToken)
        => Ok(await _auth.GetCurrentUserAsync(User.GetUserId(), cancellationToken));

    private AuthResponse ToResponse(AuthResult result)
    {
        AppendRefreshCookie(result.RefreshToken, result.RefreshTokenExpiresAt);
        return new AuthResponse(result.AccessToken, result.AccessTokenExpiresAt, result.User);
    }

    private void AppendRefreshCookie(string token, DateTimeOffset expiresAt)
    {
        Response.Cookies.Append(RefreshCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !_environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Path = RefreshCookiePath,
            Expires = expiresAt,
        });
    }

    private void DeleteRefreshCookie()
    {
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            Path = RefreshCookiePath,
            Secure = !_environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
        });
    }
}
