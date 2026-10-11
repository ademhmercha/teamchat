using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using TeamChat.IntegrationTests.Support;

namespace TeamChat.IntegrationTests.Api;

[Collection("api")]
public sealed class UnreadTests
{
    private readonly ApiCollectionFixture _fixture;

    public UnreadTests(ApiCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Unread_count_tracks_new_messages_until_marked_read()
    {
        using var client = _fixture.Factory.CreateClient();
        var (_, tokenA) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "unread-a@teamchat.test");
        var (otherId, tokenB) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "unread-b@teamchat.test");
        var conversationId = await ApiTestHelper.CreateDirectAsync(client, tokenA, otherId);

        await using var connectionA = ApiTestHelper.CreateConnection(_fixture, tokenA);
        await connectionA.StartAsync();

        await connectionA.InvokeAsync("SendMessage", conversationId, "un");
        await connectionA.InvokeAsync("SendMessage", conversationId, "deux");
        await connectionA.InvokeAsync("SendMessage", conversationId, "trois");

        Assert.Equal(3, await GetUnreadAsync(client, tokenB, conversationId));

        var historyResponse = await ApiTestHelper.SendAsync(
            client, HttpMethod.Get, $"/api/conversations/{conversationId}/messages", tokenB);
        historyResponse.EnsureSuccessStatusCode();
        var history = await historyResponse.Content.ReadFromJsonAsync<MessageHistoryResult>();
        var lastId = history!.Items[0].Id;

        var read = await ApiTestHelper.SendAsync(
            client, HttpMethod.Post, $"/api/conversations/{conversationId}/read", tokenB, new { messageId = lastId });
        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);

        Assert.Equal(0, await GetUnreadAsync(client, tokenB, conversationId));

        await connectionA.InvokeAsync("SendMessage", conversationId, "quatre");
        Assert.Equal(1, await GetUnreadAsync(client, tokenB, conversationId));

        var readAll = await ApiTestHelper.SendAsync(
            client, HttpMethod.Post, $"/api/conversations/{conversationId}/read", tokenB, new { });
        Assert.Equal(HttpStatusCode.NoContent, readAll.StatusCode);
        Assert.Equal(0, await GetUnreadAsync(client, tokenB, conversationId));
    }

    private static async Task<int> GetUnreadAsync(HttpClient client, string accessToken, Guid conversationId)
    {
        var response = await ApiTestHelper.SendAsync(client, HttpMethod.Get, "/api/conversations", accessToken);
        response.EnsureSuccessStatusCode();
        var conversations = await response.Content.ReadFromJsonAsync<List<ConversationListItemDto>>();
        var conversation = conversations!.Single(c => c.Id == conversationId);
        return conversation.UnreadCount;
    }
}
