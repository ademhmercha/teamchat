using Testcontainers.PostgreSql;

namespace TeamChat.IntegrationTests.Support;

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiCollectionFixture>
{
}

public sealed class ApiCollectionFixture : IAsyncLifetime
{
    public const string AdminEmail = "admin@teamchat.test";
    public const string AdminPassword = "Admin123!";
    public const string SuperAdminEmail = "superadmin@teamchat.test";
    public const string SuperAdminPassword = "SuperAdmin123!";

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase($"teamchat_test_{Guid.NewGuid():N}")
        .WithUsername("teamchat")
        .WithPassword("teamchat_test")
        .Build();

    public TeamChatApiFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _db.StartAsync().ConfigureAwait(false);
        Factory = new TeamChatApiFactory(_db.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync().ConfigureAwait(false);
        await _db.DisposeAsync().ConfigureAwait(false);
    }
}