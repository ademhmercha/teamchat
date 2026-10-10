using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Auth.Dtos;
using TeamChat.IntegrationTests.Support;

namespace TeamChat.IntegrationTests.Api;

[Collection("api")]
public sealed class AuthEndpointTests
{
    private const string UserPassword = "Password123!";

    private readonly ApiCollectionFixture _fixture;

    public AuthEndpointTests(ApiCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Login_with_valid_credentials_returns_access_token()
    {
        using var client = _fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(ApiCollectionFixture.AdminEmail, ApiCollectionFixture.AdminPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth!.AccessToken));
        Assert.NotNull(auth.User);
        Assert.Equal(ApiCollectionFixture.AdminEmail, auth.User!.Email);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        using var client = _fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(ApiCollectionFixture.AdminEmail, "MauvaisMotDePasse!1"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_unknown_email_returns_401()
    {
        using var client = _fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("inconnu@teamchat.test", UserPassword));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_locks_account_after_five_failures()
    {
        var email = "verrouille@teamchat.test";
        await ProvisionUserAsync(email);

        using var client = _fixture.Factory.CreateClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failed = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "MauvaisMotDePasse!1"));
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        var blocked = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, UserPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, blocked.StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_token_and_rejects_reuse_of_old_token()
    {
        var email = "rotation@teamchat.test";
        await ProvisionUserAsync(email);

        using var loginClient = _fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var login = await loginClient.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, UserPassword));
        login.EnsureSuccessStatusCode();
        var originalRefresh = ReadRefreshCookie(login);

        using var refreshClient = _fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var first = await SendRefreshAsync(refreshClient, originalRefresh);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var rotated = ReadRefreshCookie(first);
        Assert.NotEqual(originalRefresh, rotated);

        var reuse = await SendRefreshAsync(refreshClient, originalRefresh);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendRefreshAsync(HttpClient client, string refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", $"tc_refresh={refreshToken}");
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

    private async Task ProvisionUserAsync(string email)
    {
        using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();

        var user = await identity.FindByEmailAsync(email, CancellationToken.None);
        if (user is null)
        {
            user = new TeamChat.Domain.Entities.AppUser
            {
                UserName = email,
                Email = email,
                DisplayName = "Compte de test",
                IsActive = true,
                EmailConfirmed = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            var created = await identity.CreateAsync(user, CancellationToken.None);
            Assert.True(created.Succeeded);
        }

        var token = await identity.GeneratePasswordResetTokenAsync(user, CancellationToken.None);
        var reset = await identity.ResetPasswordAsync(user, token, UserPassword, CancellationToken.None);
        Assert.True(reset.Succeeded);
    }
}
