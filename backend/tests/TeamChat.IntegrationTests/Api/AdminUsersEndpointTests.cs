using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Auth.Dtos;
using TeamChat.IntegrationTests.Support;

namespace TeamChat.IntegrationTests.Api;

[Collection("api")]
public sealed class AdminUsersEndpointTests
{
    private readonly ApiCollectionFixture _fixture;

    public AdminUsersEndpointTests(ApiCollectionFixture fixture)
    {
        _fixture = fixture;
    }

[Fact]
    public async Task Create_user_as_admin_returns_created_profile()
    {
        using var client = _fixture.Factory.CreateClient();
        var adminToken = await LoginAsync(client);

        var response = await CreateUserAsync(client, adminToken, "bertrand@teamchat.test", "Bertrand P.", "User");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"STATUS={(int)response.StatusCode} BODY={body}");
        var profile = await response.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.NotNull(profile);
        Assert.Equal("bertrand@teamchat.test", profile!.Email);
        Assert.Equal("User", profile.Role);
        Assert.True(profile.IsActive);
    }

    [Fact]
    public async Task Create_user_with_invalid_role_returns_400()
    {
        using var client = _fixture.Factory.CreateClient();
        var adminToken = await LoginAsync(client);

        var response = await CreateUserAsync(client, adminToken, "carole@teamchat.test", "Carole D.", "Boss");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_user_as_non_admin_returns_403()
    {
        using var client = _fixture.Factory.CreateClient();
        var userToken = await CreateUserTokenAsync(client, "novice@teamchat.test");

        var response = await CreateUserAsync(client, userToken, "interdit@teamchat.test", "Interdit", "User");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_user_role_changes_role()
    {
        using var client = _fixture.Factory.CreateClient();
        var adminToken = await LoginAsync(client);

        var created = await CreateUserAsync(client, adminToken, "promu@teamchat.test", "Promu J.", "User");
        created.EnsureSuccessStatusCode();
        var profile = await created.Content.ReadFromJsonAsync<UserProfileDto>();

        var patch = new HttpRequestMessage(HttpMethod.Patch, $"/api/admin/users/{profile!.Id}")
        {
            Content = new StringContent("{\"role\":\"Admin\"}", System.Text.Encoding.UTF8, "application/json"),
        };
        patch.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.SendAsync(patch);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.NotNull(updated);
        Assert.Equal("Admin", updated!.Role);
    }

    [Fact]
    public async Task Create_duplicate_email_returns_409()
    {
        using var client = _fixture.Factory.CreateClient();
        var adminToken = await LoginAsync(client);

        var payload = new { email = "doublon@teamchat.test", displayName = "Doublon", role = "User" };
        var first = await CreateUserAsync(client, adminToken, payload.email, payload.displayName, payload.role);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var duplicate = await CreateUserAsync(client, adminToken, payload.email, payload.displayName, payload.role);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Update_user_deactivation_revokes_sessions()
    {
        using var client = _fixture.Factory.CreateClient();
        var (userId, userToken) = await CreateUserAndLoginAsync(client, "session@teamchat.test");

        var adminToken = await LoginAsync(client);
        var adminUpdate = new HttpRequestMessage(HttpMethod.Patch, $"/api/admin/users/{userId}")
        {
            Content = new StringContent("{\"isActive\":false}", System.Text.Encoding.UTF8, "application/json"),
        };
        adminUpdate.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var patch = await client.SendAsync(adminUpdate);
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        var response = await client.SendAsync(me);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<string> LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(ApiCollectionFixture.AdminEmail, ApiCollectionFixture.AdminPassword));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.AccessToken;
    }

    private static Task<HttpResponseMessage> CreateUserAsync(
        HttpClient client,
        string accessToken,
        string email,
        string displayName,
        string role)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/users")
        {
            Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(
                    new { email, displayName, role },
                    new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),
                System.Text.Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    private async Task<(Guid UserId, string AccessToken)> CreateUserAndLoginAsync(HttpClient client, string email)
    {
        var adminToken = await LoginAsync(client);
        var response = await CreateUserAsync(client, adminToken, email, "Session User", "User");
        response.EnsureSuccessStatusCode();
        var profile = await response.Content.ReadFromJsonAsync<UserProfileDto>();

        var userToken = await CreateUserTokenAsync(client, email);
        return (profile!.Id, userToken);
    }

    private async Task<string> CreateUserTokenAsync(HttpClient client, string email)
    {
        using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();

        var user = await identity.FindByEmailAsync(email, CancellationToken.None);
        if (user is null)
        {
            var adminToken = await LoginAsync(client);
            var created = await CreateUserAsync(client, adminToken, email, "Nouveau C.", "User");
            created.EnsureSuccessStatusCode();
            user = await identity.FindByEmailAsync(email, CancellationToken.None);
        }

        var token = await identity.GeneratePasswordResetTokenAsync(user!, CancellationToken.None);
        var reset = await identity.ResetPasswordAsync(user!, token, "Password123!", CancellationToken.None);

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "Password123!"));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.AccessToken;
    }
}