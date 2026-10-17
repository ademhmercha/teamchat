using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TeamChat.Application.Common;
using TeamChat.Infrastructure.Calls;

namespace TeamChat.UnitTests.Calls;

/// <summary>
/// Configuration ICE : STUN toujours present, TURN ajoute uniquement si un secret
/// est configure, avec des identifiants temporaires (TURN REST API / HMAC-SHA1).
/// </summary>
public sealed class TurnIceServerProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Stun_only_when_turn_not_configured()
    {
        var provider = Build(new TurnOptions { StunUrls = "stun:stun.l.google.com:19302" });

        var servers = await provider.GetIceServersAsync(Guid.NewGuid());

        Assert.Single(servers);
        Assert.Equal(new[] { "stun:stun.l.google.com:19302" }, servers[0].Urls);
        Assert.Null(servers[0].Username);
        Assert.Null(servers[0].Credential);
    }

    [Fact]
    public async Task Turn_is_ignored_when_secret_is_missing()
    {
        var provider = Build(new TurnOptions
        {
            StunUrls = "stun:stun.l.google.com:19302",
            Urls = "turn:turn.example.com:3478",
            Secret = string.Empty,
        });

        var servers = await provider.GetIceServersAsync(Guid.NewGuid());

        Assert.Single(servers);
        Assert.DoesNotContain(servers, server => server.Urls.Any(url => url.StartsWith("turn:", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Turn_uses_ephemeral_hmac_credentials_matching_the_shared_secret()
    {
        const string secret = "s3cr3t-shared-with-coturn";
        var provider = Build(new TurnOptions
        {
            StunUrls = "stun:stun.l.google.com:19302",
            Urls = "turn:turn.example.com:3478?transport=udp,turn:turn.example.com:3478?transport=tcp",
            Secret = secret,
            CredentialTtlSeconds = 3600,
        });

        var servers = await provider.GetIceServersAsync(Guid.NewGuid());

        Assert.Equal(2, servers.Count);

        var turn = servers[1];
        Assert.Equal(2, turn.Urls.Count);
        Assert.Equal("turn:turn.example.com:3478?transport=udp", turn.Urls[0]);
        Assert.Equal("turn:turn.example.com:3478?transport=tcp", turn.Urls[1]);

        // username = horodatage d'expiration Unix (now + TTL).
        var expectedUsername = Now.AddSeconds(3600).ToUnixTimeSeconds().ToString();
        Assert.Equal(expectedUsername, turn.Username);

        // credential = base64(HMAC-SHA1(secret, username)).
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(secret));
        var expectedCredential = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(expectedUsername)));
        Assert.Equal(expectedCredential, turn.Credential);
    }

    [Fact]
    public async Task Turn_credentials_expire_after_the_configured_ttl()
    {
        var provider = Build(new TurnOptions
        {
            StunUrls = string.Empty,
            Urls = "turn:turn.example.com:3478",
            Secret = "secret",
            CredentialTtlSeconds = 120,
        });

        var servers = await provider.GetIceServersAsync(Guid.NewGuid());

        var turn = Assert.Single(servers);
        Assert.Equal(Now.AddSeconds(120).ToUnixTimeSeconds().ToString(), turn.Username);
    }

    private static TurnIceServerProvider Build(TurnOptions options)
        => new(Options.Create(options), new FixedTimeProvider(Now));

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
