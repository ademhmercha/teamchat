using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using TeamChat.IntegrationTests.Support;

namespace TeamChat.IntegrationTests.Api;

[Collection("api")]
public sealed class MessageEditDeleteTests
{
    private readonly ApiCollectionFixture _fixture;

    public MessageEditDeleteTests(ApiCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Author_can_edit_then_delete_message_and_others_are_notified()
    {
        using var client = _fixture.Factory.CreateClient();
        var (_, tokenA) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "edit-author@teamchat.test");
        var (otherId, tokenB) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "edit-other@teamchat.test");
        var conversationId = await ApiTestHelper.CreateDirectAsync(client, tokenA, otherId);

        await using var connectionA = ApiTestHelper.CreateConnection(_fixture, tokenA);
        await using var connectionB = ApiTestHelper.CreateConnection(_fixture, tokenB);

        var received = new TaskCompletionSource<MessageDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var edited = new TaskCompletionSource<MessageDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var deleted = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);

        connectionB.On<MessageDto>("ReceiveMessage", m =>
        {
            if (m.ConversationId == conversationId)
            {
                received.TrySetResult(m);
            }
        });
        connectionB.On<MessageDto>("MessageEdited", m =>
        {
            if (m.ConversationId == conversationId)
            {
                edited.TrySetResult(m);
            }
        });
        connectionB.On<Guid, Guid>("MessageDeleted", (_, messageId) => deleted.TrySetResult(messageId));

        await connectionA.StartAsync();
        await connectionB.StartAsync();

        await connectionA.InvokeAsync("SendMessage", conversationId, "message original");
        var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await connectionA.InvokeAsync("EditMessage", message.Id, "message modifie");
        var editedMessage = await edited.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal("message modifie", editedMessage.Content);
        Assert.True(editedMessage.Edited);

        await connectionA.InvokeAsync("DeleteMessage", message.Id);
        var deletedId = await deleted.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(message.Id, deletedId);

        var historyResponse = await ApiTestHelper.SendAsync(
            client, HttpMethod.Get, $"/api/conversations/{conversationId}/messages", tokenB);
        historyResponse.EnsureSuccessStatusCode();
        var history = await historyResponse.Content.ReadFromJsonAsync<MessageHistoryResult>();
        var stored = Assert.Single(history!.Items, i => i.Id == message.Id);
        Assert.True(stored.Deleted);
        Assert.Null(stored.Content);
    }

    [Fact]
    public async Task Non_author_cannot_edit_or_delete()
    {
        using var client = _fixture.Factory.CreateClient();
        var (_, tokenA) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "owner-msg@teamchat.test");
        var (otherId, tokenB) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "intruder-msg@teamchat.test");
        var conversationId = await ApiTestHelper.CreateDirectAsync(client, tokenA, otherId);

        await using var connectionA = ApiTestHelper.CreateConnection(_fixture, tokenA);
        await using var connectionB = ApiTestHelper.CreateConnection(_fixture, tokenB);

        var received = new TaskCompletionSource<MessageDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        connectionB.On<MessageDto>("ReceiveMessage", m =>
        {
            if (m.ConversationId == conversationId)
            {
                received.TrySetResult(m);
            }
        });

        await connectionA.StartAsync();
        await connectionB.StartAsync();

        await connectionA.InvokeAsync("SendMessage", conversationId, "a moi");
        var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await Assert.ThrowsAsync<HubException>(async () =>
            await connectionB.InvokeAsync("EditMessage", message.Id, "usurpation"));

        await Assert.ThrowsAsync<HubException>(async () =>
            await connectionB.InvokeAsync("DeleteMessage", message.Id));

        var stillThere = await ApiTestHelper.SendAsync(
            client, HttpMethod.Get, $"/api/conversations/{conversationId}/messages", tokenA);
        stillThere.EnsureSuccessStatusCode();
        var history = await stillThere.Content.ReadFromJsonAsync<MessageHistoryResult>();
        var stored = Assert.Single(history!.Items, i => i.Id == message.Id);
        Assert.False(stored.Deleted);
        Assert.Equal("a moi", stored.Content);
    }
}
