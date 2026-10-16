using System.Net;
using System.Net.Http.Json;
using TeamChat.Application.Auth.Dtos;
using TeamChat.IntegrationTests.Support;

namespace TeamChat.IntegrationTests.Api;

/// <summary>
/// Cycle de vie administrateur d'un compte : suppression logique et
/// reinitialisation du mot de passe.
/// </summary>
[Collection("api")]
public sealed class AdminUserLifecycleTests
{
    private readonly ApiCollectionFixture _fixture;

    public AdminUserLifecycleTests(ApiCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Delete_user_soft_deletes_and_blocks_login()
    {
        using var client = _fixture.Factory.CreateClient();
        var email = $"supprime{Guid.NewGuid():N}@teamchat.test";
        var (userId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, email);
        var adminToken = await AdminTokenAsync(client);

        var delete = await ApiTestHelper.SendAsync(
            client, HttpMethod.Delete, $"/api/admin/users/{userId}", adminToken);

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Delete_user_removes_from_admin_list()
    {
        using var client = _fixture.Factory.CreateClient();
        var email = $"cible{Guid.NewGuid():N}@teamchat.test";
        var (userId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, email);
        var adminToken = await AdminTokenAsync(client);

        var delete = await ApiTestHelper.SendAsync(
            client, HttpMethod.Delete, $"/api/admin/users/{userId}", adminToken);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var list = await ApiTestHelper.SendAsync(
            client, HttpMethod.Get, $"/api/admin/users?search={Uri.EscapeDataString(email)}", adminToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = await list.Content.ReadFromJsonAsync<PagedResult<UserProfileDto>>();
        Assert.NotNull(page);
        Assert.Empty(page!.Items);
    }

    [Fact]
    public async Task Delete_unknown_user_returns_404()
    {
        using var client = _fixture.Factory.CreateClient();
        var adminToken = await AdminTokenAsync(client);

        var delete = await ApiTestHelper.SendAsync(
            client, HttpMethod.Delete, $"/api/admin/users/{Guid.NewGuid()}", adminToken);

        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Fact]
    public async Task Delete_as_non_admin_returns_403()
    {
        using var client = _fixture.Factory.CreateClient();
        var (userId, userToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(
            _fixture, client, $"novice{Guid.NewGuid():N}@teamchat.test");

        var delete = await ApiTestHelper.SendAsync(
            client, HttpMethod.Delete, $"/api/admin/users/{userId}", userToken);

        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Reset_password_existing_user_sets_new_password()
    {
        using var client = _fixture.Factory.CreateClient();
        var email = $"reset{Guid.NewGuid():N}@teamchat.test";
        var (userId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, email);
        var adminToken = await AdminTokenAsync(client);

        const string newPassword = "Nouveau123!";
        var reset = await ApiTestHelper.SendAsync(
            client,
            HttpMethod.Post,
            $"/api/admin/users/{userId}/reset-password",
            adminToken,
            new { password = newPassword });

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, newPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Reset_password_unknown_user_returns_404()
    {
        using var client = _fixture.Factory.CreateClient();
        var adminToken = await AdminTokenAsync(client);

        var reset = await ApiTestHelper.SendAsync(
            client,
            HttpMethod.Post,
            $"/api/admin/users/{Guid.NewGuid()}/reset-password",
            adminToken,
            new { password = "Nouveau123!" });

        Assert.Equal(HttpStatusCode.NotFound, reset.StatusCode);
    }

    [Fact]
    public async Task Reset_password_as_non_admin_returns_403()
    {
        using var client = _fixture.Factory.CreateClient();
        var (userId, userToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(
            _fixture, client, $"novice{Guid.NewGuid():N}@teamchat.test");

        var reset = await ApiTestHelper.SendAsync(
            client,
            HttpMethod.Post,
            $"/api/admin/users/{userId}/reset-password",
            userToken,
            new { password = "Nouveau123!" });

        Assert.Equal(HttpStatusCode.Forbidden, reset.StatusCode);
    }

    private static async Task<string> AdminTokenAsync(HttpClient client)
        => await ApiTestHelper.LoginAsync(client, ApiCollectionFixture.AdminEmail, ApiCollectionFixture.AdminPassword);
}
