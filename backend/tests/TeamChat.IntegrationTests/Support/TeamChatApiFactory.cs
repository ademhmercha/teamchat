using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace TeamChat.IntegrationTests.Support;

/// <summary>Fabrique d'API parametree sur une base de test isolee (Postgres dans un conteneur).</summary>
public sealed class TeamChatApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string? _redisConnectionString;
    private readonly string _uploadsPath = Path.Combine(Path.GetTempPath(), "teamchat-test-uploads", Guid.NewGuid().ToString("N"));

    public TeamChatApiFactory(string connectionString, string? redisConnectionString = null)
    {
        _connectionString = connectionString;
        _redisConnectionString = redisConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _connectionString,
                ["Jwt:Issuer"] = "https://teamchat.test",
                ["Jwt:Audience"] = "TeamChatApiTests",
                ["Jwt:Secret"] = "test-secret-0123456789-0123456789-0123456789-0123456789",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7",
                ["Admin:SeedEmail"] = ApiCollectionFixture.AdminEmail,
                ["Admin:SeedPassword"] = ApiCollectionFixture.AdminPassword,
                ["SuperAdmin:SeedEmail"] = ApiCollectionFixture.SuperAdminEmail,
                ["SuperAdmin:SeedPassword"] = ApiCollectionFixture.SuperAdminPassword,
                ["Tenant:DefaultName"] = "TeamChat",
                ["Cors:AllowedOrigins"] = "http://localhost:4200",
                ["RateLimiting:LoginPermitLimit"] = "10000",
                ["RateLimiting:ChatPermitLimit"] = "100000",
                ["Redis:ConnectionString"] = _redisConnectionString ?? string.Empty,
                ["Storage:Provider"] = "Local",
                ["Storage:LocalPath"] = _uploadsPath,
                ["Storage:MaxFileSizeBytes"] = "4096",
                ["Storage:MaxVoiceSizeBytes"] = "4096",
            });
        });
    }
}