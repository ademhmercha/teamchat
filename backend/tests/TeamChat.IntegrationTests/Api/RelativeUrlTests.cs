using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using TeamChat.Infrastructure.Persistence;
using TeamChat.Infrastructure.Persistence.Migrations;
using TeamChat.IntegrationTests.Support;

namespace TeamChat.IntegrationTests.Api;

/// <summary>
/// Derriere CloudFront, Request.Scheme/Request.Host valent l'origine EC2 en http : l'API ne doit donc jamais
/// renvoyer d'URL absolue vers son propre hote (le navigateur la bloque : Mixed Content).
/// </summary>
[Collection("api")]
public sealed class RelativeUrlTests
{
    private const string LegacyHost = "http://ec2-16-170-120-215.eu-north-1.compute.amazonaws.com";

    private static readonly byte[] TinyPng =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
    };

    private readonly ApiCollectionFixture _fixture;

    public RelativeUrlTests(ApiCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Upload_avatar_returns_relative_path_and_no_absolute_url()
    {
        using var client = _fixture.Factory.CreateClient();
        var (userId, token) = await ApiTestHelper.ProvisionUserAndLoginAsync(
            _fixture, client, $"rel{Guid.NewGuid():N}@teamchat.test");

        var upload = await SendAvatarAsync(client, token);
        var body = await upload.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        Assert.DoesNotContain("http://", body);
        Assert.DoesNotContain("localhost", body);
        using var json = JsonDocument.Parse(body);
        Assert.Equal($"/api/users/{userId}/avatar", json.RootElement.GetProperty("avatarUrl").GetString());
    }

    [Fact]
    public async Task Legacy_absolute_avatar_in_database_is_served_as_relative_path()
    {
        using var client = _fixture.Factory.CreateClient();
        var email = $"legacy{Guid.NewGuid():N}@teamchat.test";
        var (userId, token) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, email);
        await SetAvatarRawAsync(userId, $"{LegacyHost}/api/users/{userId}/avatar");

        // Lecture unitaire, annuaire (recherche unique : evite le cache de l'annuaire) et profil courant.
        var single = await ApiTestHelper.SendAsync(client, HttpMethod.Get, $"/api/users/{userId}", token);
        var directory = await ApiTestHelper.SendAsync(client, HttpMethod.Get, $"/api/users?search={email}", token);

        foreach (var response in new[] { single, directory })
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("http://", body);
            Assert.Contains($"/api/users/{userId}/avatar", body);
        }
    }

    [Fact]
    public async Task External_avatar_url_is_left_untouched()
    {
        using var client = _fixture.Factory.CreateClient();
        var (userId, token) = await ApiTestHelper.ProvisionUserAndLoginAsync(
            _fixture, client, $"ext{Guid.NewGuid():N}@teamchat.test");
        await SetAvatarRawAsync(userId, "https://lh3.googleusercontent.com/a/photo");

        var response = await ApiTestHelper.SendAsync(client, HttpMethod.Get, $"/api/users/{userId}", token);

        Assert.Contains("https://lh3.googleusercontent.com/a/photo", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Migration_rewrites_legacy_rows_and_keeps_external_urls()
    {
        using var client = _fixture.Factory.CreateClient();
        var (legacyId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(
            _fixture, client, $"mig{Guid.NewGuid():N}@teamchat.test");
        var (externalId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(
            _fixture, client, $"migext{Guid.NewGuid():N}@teamchat.test");
        await SetAvatarRawAsync(legacyId, $"{LegacyHost.ToUpperInvariant().Replace("HTTP", "http")}/api/users/{legacyId}/avatar?t=123");
        await SetAvatarRawAsync(externalId, "https://lh3.googleusercontent.com/a/photo");

        using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TeamChatDbContext>();
        foreach (var operation in new NormalizeAvatarUrls().UpOperations.OfType<SqlOperation>())
        {
            // ExecuteSqlRaw traite {n} comme un parametre de format ; le migrateur EF, lui, envoie le SQL tel quel.
            await db.Database.ExecuteSqlRawAsync(operation.Sql.Replace("{", "{{").Replace("}", "}}"));
        }

        Assert.Equal($"/api/users/{legacyId}/avatar", await ReadAvatarRawAsync(db, legacyId));
        Assert.Equal("https://lh3.googleusercontent.com/a/photo", await ReadAvatarRawAsync(db, externalId));
    }

    [Fact]
    public async Task Admin_create_user_location_header_is_relative()
    {
        using var client = _fixture.Factory.CreateClient();
        var adminToken = await ApiTestHelper.LoginAsync(
            client, ApiCollectionFixture.AdminEmail, ApiCollectionFixture.AdminPassword);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/users")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new
                    {
                        email = $"loc{Guid.NewGuid():N}@teamchat.test",
                        displayName = "Location Test",
                        role = "User",
                        password = "Password123!",
                    },
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.False(response.Headers.Location!.IsAbsoluteUri, $"Location absolue : {response.Headers.Location}");
        Assert.StartsWith("/api/users/", response.Headers.Location.OriginalString);
    }

    private async Task SetAvatarRawAsync(Guid userId, string value)
    {
        // SQL brut : contourne le convertisseur EF pour simuler une ligne ecrite par l'ancien code.
        using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TeamChatDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE \"AspNetUsers\" SET \"AvatarUrl\" = {0} WHERE \"Id\" = {1}", value, userId);
    }

    private static Task<string?> ReadAvatarRawAsync(TeamChatDbContext db, Guid userId)
        => db.Database
            .SqlQuery<string>($"SELECT \"AvatarUrl\" AS \"Value\" FROM \"AspNetUsers\" WHERE \"Id\" = {userId}")
            .SingleOrDefaultAsync();

    private static async Task<HttpResponseMessage> SendAvatarAsync(HttpClient client, string token)
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(TinyPng);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "file", "photo.png");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/users/me/avatar") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }
}
