using Microsoft.AspNetCore.SignalR.Client;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using TeamChat.IntegrationTests.Support;

namespace TeamChat.IntegrationTests.Api;

[CollectionDefinition("redis")]
public sealed class RedisBackplaneCollection : ICollectionFixture<RedisBackplaneFixture>
{
}

public sealed class RedisBackplaneFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase($"teamchat_redis_{Guid.NewGuid():N}")
        .WithUsername("teamchat")
        .WithPassword("teamchat_test")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    public TeamChatApiFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_db.StartAsync(), _redis.StartAsync()).ConfigureAwait(false);
        Factory = new TeamChatApiFactory(_db.GetConnectionString(), _redis.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync().ConfigureAwait(false);
        await _db.DisposeAsync().ConfigureAwait(false);
        await _redis.DisposeAsync().ConfigureAwait(false);
    }
}

[Collection("redis")]
public sealed class RedisBackplaneTests
{
    private readonly RedisBackplaneFixture _fixture;

    public RedisBackplaneTests(RedisBackplaneFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Messaging_works_with_redis_backplane_and_cache()
    {
        using var client = _fixture.Factory.CreateClient();
        var (_, tokenA) = await ApiTestHelper.ProvisionUserAndLoginAsync(
            _fixture.Factory, client, $"redisa{Guid.NewGuid():N}@teamchat.test");
        var (peerId, tokenB) = await ApiTestHelper.ProvisionUserAndLoginAsync(
            _fixture.Factory, client, $"redisb{Guid.NewGuid():N}@teamchat.test");

        var conversationId = await ApiTestHelper.CreateDirectAsync(client, tokenA, peerId);

        await using var connectionA = ApiTestHelper.CreateConnection(_fixture.Factory, tokenA);
        await using var connectionB = ApiTestHelper.CreateConnection(_fixture.Factory, tokenB);

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

        await connectionA.InvokeAsync("SendMessage", conversationId, "via backplane redis");

        var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal("via backplane redis", message.Content);
    }
}
