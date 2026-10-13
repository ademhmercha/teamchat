using System.Net;
using Microsoft.AspNetCore.SignalR.Client;
using TeamChat.IntegrationTests.Support;

namespace TeamChat.IntegrationTests.Api;

[Collection("api")]
public sealed class AdminUserDeactivationTests
{
    private readonly ApiCollectionFixture _fixture;

    public AdminUserDeactivationTests(ApiCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Deactivation_forces_signalr_logout_and_updates_presence()
    {
        using var client = _fixture.Factory.CreateClient();
        var (userId, userToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(
            _fixture, client, $"forcedlogout{Guid.NewGuid():N}@teamchat.test");
        var (_, observerToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(
            _fixture, client, $"observer{Guid.NewGuid():N}@teamchat.test");
        var adminToken = await ApiTestHelper.LoginAsync(
            client, ApiCollectionFixture.AdminEmail, ApiCollectionFixture.AdminPassword);

        await using var userConnection = ApiTestHelper.CreateConnection(_fixture, userToken);
        await using var observerConnection = ApiTestHelper.CreateConnection(_fixture, observerToken);

        var forcedLogout = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var wentOffline = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        userConnection.On("ForceLogout", () => forcedLogout.TrySetResult());
        observerConnection.On<Guid, bool>("PresenceChanged", (changedUserId, isOnline) =>
        {
            if (changedUserId == userId && !isOnline)
            {
                wentOffline.TrySetResult();
            }
        });

        await observerConnection.StartAsync();
        await userConnection.StartAsync();

        var patch = await ApiTestHelper.SendAsync(
            client, HttpMethod.Patch, $"/api/admin/users/{userId}", adminToken, new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        await forcedLogout.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await wentOffline.Task.WaitAsync(TimeSpan.FromSeconds(15));
    }
}
