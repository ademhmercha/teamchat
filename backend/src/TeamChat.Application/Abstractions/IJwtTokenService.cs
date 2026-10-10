using TeamChat.Domain.Entities;

namespace TeamChat.Application.Abstractions;

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

public interface IJwtTokenService
{
    AccessToken CreateAccessToken(AppUser user, IEnumerable<string> roles);
}
