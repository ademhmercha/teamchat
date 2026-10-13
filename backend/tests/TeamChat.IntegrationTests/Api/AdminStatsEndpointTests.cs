using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using TeamChat.IntegrationTests.Support;

namespace TeamChat.IntegrationTests.Api;

[Collection("api")]
public sealed class AdminStatsEndpointTests
{
    private readonly ApiCollectionFixture _fixture;

    public AdminStatsEndpointTests(ApiCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Stats_requires_admin_role()
    {
        using var client = _fixture.Factory.CreateClient();
        var (_, userToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(
            _fixture, client, $"statsuser{Guid.NewGuid():N}@teamchat.test");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/stats");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Stats_reports_users_message_trend_and_counts_recent_messages()
    {
        using var client = _fixture.Factory.CreateClient();
        var adminToken = await ApiTestHelper.LoginAsync(
            client, ApiCollectionFixture.AdminEmail, ApiCollectionFixture.AdminPassword);
        var (peerId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(
            _fixture, client, $"statsp{Guid.NewGuid():N}@teamchat.test");

        var conversationId = await ApiTestHelper.CreateDirectAsync(client, adminToken, peerId);

        await using var connection = ApiTestHelper.CreateConnection(_fixture, adminToken);
        await connection.StartAsync();
        await connection.InvokeAsync("SendMessage", conversationId, "Message mesure par les stats");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/stats");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var stats = await response.Content.ReadFromJsonAsync<AdminStatsDto>();

        Assert.NotNull(stats);
        Assert.True(stats!.ActiveUsers >= 1, "Au moins l'admin doit etre actif.");
        Assert.True(stats.TotalUsers >= stats.ActiveUsers);
        Assert.Equal(7, stats.MessagesPerDay.Count);
        Assert.True(stats.MessagesLast7Days >= 1, "Le message envoye doit etre compte.");
        Assert.Equal(stats.MessagesPerDay.Sum(d => d.Count), stats.MessagesLast7Days);
    }
}
