using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Calls.Dtos;
using TeamChat.Application.Common;

namespace TeamChat.Infrastructure.Calls;

/// <summary>
/// Fournit la configuration ICE : serveurs STUN publics et, si configure, TURN
/// avec des identifiants temporaires derives du secret partage (mecanisme
/// "TURN REST API" de coturn, option use-auth-secret).
/// </summary>
public sealed class TurnIceServerProvider : IIceServerProvider
{
    private readonly TurnOptions _options;
    private readonly TimeProvider _time;

    public TurnIceServerProvider(IOptions<TurnOptions> options, TimeProvider time)
    {
        _options = options.Value;
        _time = time;
    }

    public Task<IReadOnlyList<IceServerDto>> GetIceServersAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var servers = new List<IceServerDto>();

        var stun = Split(_options.StunUrls);
        if (stun.Count > 0)
        {
            servers.Add(new IceServerDto(stun, null, null));
        }

        var turn = Split(_options.Urls);
        if (turn.Count > 0 && !string.IsNullOrWhiteSpace(_options.Secret))
        {
            var (username, credential) = CreateCredentials(_time.GetUtcNow());
            servers.Add(new IceServerDto(turn, username, credential));
        }

        return Task.FromResult<IReadOnlyList<IceServerDto>>(servers);
    }

    private (string Username, string Credential) CreateCredentials(DateTimeOffset now)
    {
        var username = now
            .AddSeconds(_options.CredentialTtlSeconds)
            .ToUnixTimeSeconds()
            .ToString(CultureInfo.InvariantCulture);

        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(_options.Secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(username));
        var credential = Convert.ToBase64String(hash);

        return (username, credential);
    }

    private static IReadOnlyList<string> Split(string value)
        => string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
