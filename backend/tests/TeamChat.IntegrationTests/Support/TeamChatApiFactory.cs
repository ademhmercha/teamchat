using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace TeamChat.IntegrationTests.Support;

/// <summary>Fabrique d'API parametree sur une base de test isolee (Postgres dans un conteneur).</summary>
public sealed class TeamChatApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public TeamChatApiFactory(string connectionString)
    {
        _connectionString = connectionString;
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
                ["Cors:AllowedOrigins"] = "http://localhost:4200",
                ["RateLimiting:LoginPermitLimit"] = "100",
            });
        });
    }
}