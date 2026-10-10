using System.Net;
using System.Net.Http.Json;
using TeamChat.IntegrationTests.Support;

namespace TeamChat.IntegrationTests.Api;

[Collection("api")]
public sealed class HealthEndpointTests
{
    private readonly ApiCollectionFixture _fixture;

    public HealthEndpointTests(ApiCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Health_endpoint_returns_ok_with_cache_resolved()
    {
        using var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(payload);
        Assert.Equal("ok", payload!.Status);
        Assert.Equal("TeamChat.Api", payload.Service);
        Assert.Equal("HybridCacheService", payload.Cache);
    }

    public sealed record HealthResponse(string Status, string Service, string Cache);
}