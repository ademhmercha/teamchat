using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Auth.Dtos;
using TeamChat.IntegrationTests.Support;

namespace TeamChat.IntegrationTests.Api;

[Collection("api")]
public sealed class ChatHubTests
{
    private readonly ApiCollectionFixture _fixture;

    public ChatHubTests(ApiCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Send_message_broadcasts_to_other_participant()
    {
        using var client = _fixture.Factory.CreateClient();
        var (_, tokenA) = await ProvisionUserAndLoginAsync(client, "hubA@teamchat.test");
        var (otherId, tokenB) = await ProvisionUserAndLoginAsync(client, "hubB@teamchat.test");

        var conversationId = await CreateDirectAsync(client, tokenA, otherId);

        await using var connectionA = CreateConnection(tokenA);
        await using var connectionB = CreateConnection(tokenB);
        var received = new TaskCompletionSource<MessageDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        connectionB.On<MessageDto>("ReceiveMessage", message =>
        {
            if (message.ConversationId == conversationId)
            {
                received.TrySetResult(message);
            }
        });

        await connectionA.StartAsync();
        await connectionB.StartAsync();

        await connectionA.InvokeAsync("SendMessage", conversationId, "Bonjour via hub");

        var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal("Bonjour via hub", message.Content);
        Assert.Equal(conversationId, message.ConversationId);
    }

    [Fact]
    public async Task Presence_changes_are_broadcast_and_snapshot_sent()
    {
        using var client = _fixture.Factory.CreateClient();
        var (_, tokenA) = await ProvisionUserAndLoginAsync(client, "presenceA@teamchat.test");
        var (otherId, tokenB) = await ProvisionUserAndLoginAsync(client, "presenceB@teamchat.test");

        await using var connectionA = CreateConnection(tokenA);
        var online = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var offline = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var snapshot = new TaskCompletionSource<IReadOnlyList<Guid>>(TaskCreationOptions.RunContinuationsAsynchronously);

        connectionA.On<Guid, bool>("PresenceChanged", (userId, isOnline) =>
        {
            if (userId == otherId)
            {
                (isOnline ? online : offline).TrySetResult(isOnline);
            }
        });
        connectionA.On<IReadOnlyList<Guid>>("PresenceSnapshot", ids => snapshot.TrySetResult(ids));

        await connectionA.StartAsync();

        await using var connectionB = CreateConnection(tokenB);
        await connectionB.StartAsync();

        var snapshotResult = await snapshot.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Contains(otherId, snapshotResult);

        var onlineReceived = await online.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.True(onlineReceived);

        await connectionB.DisposeAsync();
        var offlineReceived = await offline.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.False(offlineReceived);
    }

    [Fact]
    public async Task Non_member_cannot_send_message()
    {
        using var client = _fixture.Factory.CreateClient();
        var (_, tokenA) = await ProvisionUserAndLoginAsync(client, "membreHub@teamchat.test");
        var (otherId, _) = await ProvisionUserAndLoginAsync(client, "destinataireHub@teamchat.test");
        var (_, strangerToken) = await ProvisionUserAndLoginAsync(client, "intrus@teamchat.test");

        var conversationId = await CreateDirectAsync(client, tokenA, otherId);

        await using var connection = CreateConnection(strangerToken);
        await connection.StartAsync();

        await Assert.ThrowsAsync<HubException>(async () =>
            await connection.InvokeAsync("SendMessage", conversationId, "Intrusion"));
    }

    [Fact]
    public async Task Unauthenticated_connection_is_rejected()
    {
        var handler = _fixture.Factory.Server.CreateHandler();
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri("http://localhost/hubs/chat"), options =>
            {
                options.HttpMessageHandlerFactory = _ => handler;
                options.Transports = HttpTransportType.LongPolling;
            })
            .WithAutomaticReconnect()
            .Build();

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }

    private HubConnection CreateConnection(string accessToken)
    {
        var handler = _fixture.Factory.Server.CreateHandler();
        return new HubConnectionBuilder()
            .WithUrl(new Uri("http://localhost/hubs/chat"), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                options.HttpMessageHandlerFactory = _ => handler;
                options.Transports = HttpTransportType.LongPolling;
            })
            .WithAutomaticReconnect()
            .Build();
    }

    private async Task<Guid> CreateDirectAsync(HttpClient client, string accessToken, Guid otherId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/conversations/direct")
        {
            Content = JsonContent.Create(new { userId = otherId }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var conversation = await response.Content.ReadFromJsonAsync<ConversationDto>();
        return conversation!.Id;
    }

    private async Task<(Guid UserId, string AccessToken)> ProvisionUserAndLoginAsync(HttpClient client, string email)
    {
        Guid userId;
        using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();

            var user = await identity.FindByEmailAsync(email, CancellationToken.None);
            if (user is null)
            {
                var userEntity = new TeamChat.Domain.Entities.AppUser
                {
                    UserName = email,
                    Email = email,
                    DisplayName = "Hub Tester",
                    IsActive = true,
                    EmailConfirmed = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                var created = await identity.CreateAsync(userEntity, CancellationToken.None);
                Assert.True(created.Succeeded);
                user = userEntity;
            }

            var token = await identity.GeneratePasswordResetTokenAsync(user, CancellationToken.None);
            var reset = await identity.ResetPasswordAsync(user, token, "Password123!", CancellationToken.None);
            Assert.True(reset.Succeeded);
            userId = user.Id;
        }

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "Password123!"));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return (userId, auth!.AccessToken);
    }
}