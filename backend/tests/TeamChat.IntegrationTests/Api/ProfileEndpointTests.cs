using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TeamChat.Application.Auth.Dtos;
using TeamChat.IntegrationTests.Support;

namespace TeamChat.IntegrationTests.Api;

/// <summary>Profil en libre-service : nom affiche, mot de passe, deconnexion globale, avatar.</summary>
[Collection("api")]
public sealed class ProfileEndpointTests
{
    private const string InitialPassword = "Password123!";

    private static readonly byte[] TinyPng =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
    };

    private readonly ApiCollectionFixture _fixture;

    public ProfileEndpointTests(ApiCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Update_profile_changes_display_name()
    {
        using var client = _fixture.Factory.CreateClient();
        var email = $"profil{Guid.NewGuid():N}@teamchat.test";
        var (_, token) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, email);

        var response = await ApiTestHelper.SendAsync(
            client, HttpMethod.Patch, "/api/users/me", token, new { displayName = "Nouveau Nom" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<Support.UserProfileDto>();
        Assert.Equal("Nouveau Nom", profile!.DisplayName);

        var me = await ApiTestHelper.SendAsync(client, HttpMethod.Get, "/api/auth/me", token);
        var current = await me.Content.ReadFromJsonAsync<Support.UserProfileDto>();
        Assert.Equal("Nouveau Nom", current!.DisplayName);
    }

    [Fact]
    public async Task Update_profile_with_blank_name_returns_bad_request()
    {
        using var client = _fixture.Factory.CreateClient();
        var email = $"profilbad{Guid.NewGuid():N}@teamchat.test";
        var (_, token) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, email);

        var response = await ApiTestHelper.SendAsync(
            client, HttpMethod.Patch, "/api/users/me", token, new { displayName = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Change_password_with_correct_current_password_succeeds()
    {
        using var client = _fixture.Factory.CreateClient();
        var email = $"pwd{Guid.NewGuid():N}@teamchat.test";
        var (_, token) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, email);

        var response = await ApiTestHelper.SendAsync(
            client, HttpMethod.Post, "/api/auth/change-password", token,
            new { currentPassword = InitialPassword, newPassword = "Nouveau123!" });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var newLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Nouveau123!"));
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);

        var oldLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, InitialPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
    }

    [Fact]
    public async Task Change_password_with_wrong_current_password_returns_bad_request()
    {
        using var client = _fixture.Factory.CreateClient();
        var email = $"pwdwrong{Guid.NewGuid():N}@teamchat.test";
        var (_, token) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, email);

        var response = await ApiTestHelper.SendAsync(
            client, HttpMethod.Post, "/api/auth/change-password", token,
            new { currentPassword = "MauvaisActuel!1", newPassword = "Nouveau123!" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Change_password_with_weak_new_password_returns_bad_request()
    {
        using var client = _fixture.Factory.CreateClient();
        var email = $"pwdweak{Guid.NewGuid():N}@teamchat.test";
        var (_, token) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, email);

        var response = await ApiTestHelper.SendAsync(
            client, HttpMethod.Post, "/api/auth/change-password", token,
            new { currentPassword = InitialPassword, newPassword = "court" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Logout_all_revokes_refresh_token()
    {
        var email = $"logoutall{Guid.NewGuid():N}@teamchat.test";
        using var client = _fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var (_, token) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, email);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, InitialPassword));
        login.EnsureSuccessStatusCode();
        var refresh = ReadRefreshCookie(login);

        var logoutAll = await ApiTestHelper.SendAsync(client, HttpMethod.Post, "/api/auth/logout-all", token);
        Assert.Equal(HttpStatusCode.NoContent, logoutAll.StatusCode);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", $"tc_refresh={refresh}");
        var refreshResponse = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task Upload_avatar_saves_and_serves_image()
    {
        using var client = _fixture.Factory.CreateClient();
        var email = $"avatar{Guid.NewGuid():N}@teamchat.test";
        var (userId, token) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, email);

        var upload = await SendAvatarAsync(client, token, TinyPng, "image/png", "photo.png");
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var profile = await upload.Content.ReadFromJsonAsync<Support.UserProfileDto>();
        Assert.False(string.IsNullOrWhiteSpace(profile!.AvatarUrl));

        var served = await client.GetAsync($"/api/users/{userId}/avatar");
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal("image/png", served.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Upload_avatar_with_non_image_returns_bad_request()
    {
        using var client = _fixture.Factory.CreateClient();
        var email = $"avatarbad{Guid.NewGuid():N}@teamchat.test";
        var (_, token) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, email);

        var upload = await SendAvatarAsync(client, token, "pas une image"u8.ToArray(), "text/plain", "note.txt");

        Assert.Equal(HttpStatusCode.BadRequest, upload.StatusCode);
    }

    [Fact]
    public async Task Remove_avatar_clears_it()
    {
        using var client = _fixture.Factory.CreateClient();
        var email = $"avatardel{Guid.NewGuid():N}@teamchat.test";
        var (userId, token) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, email);

        await SendAvatarAsync(client, token, TinyPng, "image/png", "photo.png");

        var removed = await ApiTestHelper.SendAsync(client, HttpMethod.Delete, "/api/users/me/avatar", token);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

        var served = await client.GetAsync($"/api/users/{userId}/avatar");
        Assert.Equal(HttpStatusCode.NotFound, served.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendAvatarAsync(
        HttpClient client,
        string token,
        byte[] bytes,
        string contentType,
        string fileName)
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "file", fileName);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/users/me/avatar")
        {
            Content = content,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private static string ReadRefreshCookie(HttpResponseMessage response)
    {
        var header = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith("tc_refresh=", StringComparison.Ordinal))
            : null;

        Assert.NotNull(header);
        var pair = header!.Split(';', 2)[0];
        return pair["tc_refresh=".Length..];
    }
}
