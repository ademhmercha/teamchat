using System.Security.Claims;

namespace TeamChat.Api.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue("sub");
        return Guid.TryParse(value, out var id)
            ? id
            : throw new UnauthorizedAccessException("Identifiant utilisateur absent du token.");
    }
}
