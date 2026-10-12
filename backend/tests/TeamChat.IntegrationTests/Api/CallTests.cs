using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using TeamChat.Application.Abstractions;
using TeamChat.Domain.Entities;
using TeamChat.IntegrationTests.Support;

namespace TeamChat.IntegrationTests.Api;

[Collection("api")]
public sealed class CallTests
{
    private readonly ApiCollectionFixture _fixture;

    public CallTests(ApiCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Call_flow_persists_log_and_posts_system_message()
    {
        using var client = _fixture.Factory.CreateClient();
        var (_, callerToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "caller1@teamchat.test");
        var (calleeId, calleeToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "callee1@teamchat.test");

        await using var caller = ApiTestHelper.CreateConnection(_fixture, callerToken);
        await using var callee = ApiTestHelper.CreateConnection(_fixture, calleeToken);

        var incoming = new TaskCompletionSource<CallSessionDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        callee.On<CallSessionDto>("IncomingCall", call => incoming.TrySetResult(call));

        var accepted = new TaskCompletionSource<CallSessionDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        caller.On<CallSessionDto>("CallAccepted", call => accepted.TrySetResult(call));

        var systemMessage = new TaskCompletionSource<MessageDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        caller.On<MessageDto>("ReceiveMessage", message =>
        {
            if (message.Type == (int)MessageType.System)
            {
                systemMessage.TrySetResult(message);
            }
        });

        await caller.StartAsync();
        await callee.StartAsync();
        await WaitUntilOnlineAsync(calleeId);

        var session = await caller.InvokeAsync<CallSessionDto>("CallUser", calleeId, CallType.Audio);
        Assert.Equal((int)CallStatus.Ringing, session.Status);

        var received = await incoming.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(session.CallId, received.CallId);

        await callee.InvokeAsync("AcceptCall", session.CallId);
        var acceptedSession = await accepted.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal((int)CallStatus.Accepted, acceptedSession.Status);

        await caller.InvokeAsync("EndCall", session.CallId);

        var message = await systemMessage.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal((int)MessageType.System, message.Type);
        Assert.NotNull(message.Content);
        Assert.Contains("Appel", message.Content!, StringComparison.Ordinal);

        using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<ICallQueries>();
        var record = await queries.FindAsync(session.CallId);
        Assert.NotNull(record);
        Assert.Equal(CallStatus.Ended, record!.Status);
        Assert.NotNull(record.EndedAt);
    }

    [Fact]
    public async Task Third_party_cannot_relay_signaling()
    {
        using var client = _fixture.Factory.CreateClient();
        var (_, callerToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "caller2@teamchat.test");
        var (calleeId, calleeToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "callee2@teamchat.test");
        var (_, strangerToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "stranger2@teamchat.test");

        await using var caller = ApiTestHelper.CreateConnection(_fixture, callerToken);
        await using var callee = ApiTestHelper.CreateConnection(_fixture, calleeToken);
        await using var stranger = ApiTestHelper.CreateConnection(_fixture, strangerToken);

        var incoming = new TaskCompletionSource<CallSessionDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        callee.On<CallSessionDto>("IncomingCall", call => incoming.TrySetResult(call));

        await caller.StartAsync();
        await callee.StartAsync();
        await stranger.StartAsync();
        await WaitUntilOnlineAsync(calleeId);

        var session = await caller.InvokeAsync<CallSessionDto>("CallUser", calleeId, CallType.Audio);
        await incoming.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await callee.InvokeAsync("AcceptCall", session.CallId);

        await Assert.ThrowsAsync<HubException>(async () =>
            await stranger.InvokeAsync("SendOffer", session.CallId, "sdp-offer"));
    }

    [Fact]
    public async Task Call_to_offline_user_is_rejected()
    {
        using var client = _fixture.Factory.CreateClient();
        var (_, callerToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "caller3@teamchat.test");
        var (calleeId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "callee3@teamchat.test");

        await using var caller = ApiTestHelper.CreateConnection(_fixture, callerToken);
        await caller.StartAsync();

        await Assert.ThrowsAsync<HubException>(async () =>
            await caller.InvokeAsync<CallSessionDto>("CallUser", calleeId, CallType.Audio));
    }

    [Fact]
    public async Task Ice_servers_endpoint_returns_stun_configuration()
    {
        using var client = _fixture.Factory.CreateClient();
        var (_, token) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "ice@teamchat.test");

        var response = await ApiTestHelper.SendAsync(client, HttpMethod.Get, "/api/calls/ice-servers", token);
        response.EnsureSuccessStatusCode();

        var servers = await response.Content.ReadFromJsonAsync<List<IceServerDto>>();
        Assert.NotNull(servers);
        Assert.Contains(servers!, server => server.Urls.Any(url => url.StartsWith("stun:", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task History_endpoint_lists_recent_call()
    {
        using var client = _fixture.Factory.CreateClient();
        var (_, callerToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "historycaller@teamchat.test");
        var (calleeId, calleeToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "historycallee@teamchat.test");

        await using var caller = ApiTestHelper.CreateConnection(_fixture, callerToken);
        await using var callee = ApiTestHelper.CreateConnection(_fixture, calleeToken);

        var incoming = new TaskCompletionSource<CallSessionDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        callee.On<CallSessionDto>("IncomingCall", call => incoming.TrySetResult(call));

        await caller.StartAsync();
        await callee.StartAsync();
        await WaitUntilOnlineAsync(calleeId);

        var session = await caller.InvokeAsync<CallSessionDto>("CallUser", calleeId, CallType.Video);
        await incoming.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await callee.InvokeAsync("AcceptCall", session.CallId);
        await caller.InvokeAsync("EndCall", session.CallId);

        var response = await ApiTestHelper.SendAsync(client, HttpMethod.Get, "/api/calls/history", callerToken);
        response.EnsureSuccessStatusCode();

        var history = await response.Content.ReadFromJsonAsync<List<CallHistoryItemDto>>();
        Assert.NotNull(history);
        Assert.Contains(history!, item => item.Id == session.CallId && item.Outgoing);
    }

    private async Task WaitUntilOnlineAsync(Guid userId)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using (var scope = _fixture.Factory.Services.CreateAsyncScope())
            {
                var presence = scope.ServiceProvider.GetRequiredService<IPresenceTracker>();
                if (presence.GetOnlineUserIds().Contains(userId))
                {
                    return;
                }
            }

            await Task.Delay(50);
        }

        throw new TimeoutException($"L'utilisateur {userId} n'est jamais passe en ligne.");
    }
}
