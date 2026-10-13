using System.Net;
using Microsoft.Extensions.DependencyInjection;
using TeamChat.Application.Abstractions;
using TeamChat.IntegrationTests.Support;

namespace TeamChat.IntegrationTests.Api;

[Collection("api")]
public sealed class MembershipCacheTests
{
    private readonly ApiCollectionFixture _fixture;

    public MembershipCacheTests(ApiCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Deactivation_invalidates_cached_membership()
    {
        using var client = _fixture.Factory.CreateClient();
        var (userId, userToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(
            _fixture, client, $"cache{Guid.NewGuid():N}@teamchat.test");
        var (peerId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(
            _fixture, client, $"cachep{Guid.NewGuid():N}@teamchat.test");
        var adminToken = await ApiTestHelper.LoginAsync(
            client, ApiCollectionFixture.AdminEmail, ApiCollectionFixture.AdminPassword);

        var conversationId = await ApiTestHelper.CreateDirectAsync(client, userToken, peerId);

        using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            var membership = scope.ServiceProvider.GetRequiredService<IMembershipService>();
            Assert.True(await membership.IsMemberAsync(conversationId, userId));
        }

        var patch = await ApiTestHelper.SendAsync(
            client, HttpMethod.Patch, $"/api/admin/users/{userId}", adminToken, new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            var membership = scope.ServiceProvider.GetRequiredService<IMembershipService>();
            Assert.False(await membership.IsMemberAsync(conversationId, userId));
        }
    }
}
