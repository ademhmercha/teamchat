using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TeamChat.Application.Auth.Dtos;
using TeamChat.IntegrationTests.Support;

namespace TeamChat.IntegrationTests.Api;

[Collection("api")]
public sealed class UsersDirectoryEndpointTests
{
    private readonly ApiCollectionFixture _fixture;

    public UsersDirectoryEndpointTests(ApiCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Search_without_token_returns_401()
    {
        using var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Search_returns_seeded_admin()
    {
        using var client = _fixture.Factory.CreateClient();
        var accessToken = await LoginAsync(client);

        var response = await GetAsync(client, accessToken, "/api/users?search=admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<UserListItemDto>>();
        Assert.NotNull(page);
        Assert.True(page!.TotalCount >= 1);
        Assert.Contains(page.Items, u => u.Email == ApiCollectionFixture.AdminEmail);
    }

    [Fact]
    public async Task Get_user_by_id_returns_profile()
    {
        using var client = _fixture.Factory.CreateClient();
        var accessToken = await LoginAsync(client);

        var search = await GetAsync(client, accessToken, "/api/users?search=admin");
        search.EnsureSuccessStatusCode();
        var page = await search.Content.ReadFromJsonAsync<PagedResult<UserListItemDto>>();
        var admin = page!.Items.First(u => u.Email == ApiCollectionFixture.AdminEmail);

        var response = await GetAsync(client, accessToken, $"/api/users/{admin.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.NotNull(profile);
        Assert.Equal(ApiCollectionFixture.AdminEmail, profile!.Email);
    }

    [Fact]
    public async Task Get_unknown_user_returns_404()
    {
        using var client = _fixture.Factory.CreateClient();
        var accessToken = await LoginAsync(client);

        var response = await GetAsync(client, accessToken, $"/api/users/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string accessToken, string uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request);
    }

    private async Task<string> LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(ApiCollectionFixture.AdminEmail, ApiCollectionFixture.AdminPassword));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.AccessToken;
    }
}