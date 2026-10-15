using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Auth.Dtos;
using TeamChat.IntegrationTests.Support;

namespace TeamChat.IntegrationTests.Api;

/// <summary>Mot de passe oublie / reinitialisation en libre-service.</summary>
[Collection("api")]
public sealed class PasswordResetTests
{
    private readonly ApiCollectionFixture _fixture;

    public PasswordResetTests(ApiCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Forgot_password_unknown_email_returns_accepted()
    {
        using var client = _fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/forgot-password", new { email = $"inconnu{Guid.NewGuid():N}@teamchat.test" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Forgot_password_known_email_returns_accepted()
    {
        using var client = _fixture.Factory.CreateClient();
        var email = $"oubli{Guid.NewGuid():N}@teamchat.test";
        await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, email);

        var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Forgot_password_invalid_email_returns_bad_request()
    {
        using var client = _fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "pas-un-email" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reset_password_with_valid_token_changes_password()
    {
        using var client = _fixture.Factory.CreateClient();
        var email = $"resetok{Guid.NewGuid():N}@teamchat.test";
        var (userId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, email);
        var token = await GenerateTokenAsync(email);

        var reset = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new { userId, token, password = "Nouveau123!" });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        var newLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Nouveau123!"));
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);

        var oldLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
    }

    [Fact]
    public async Task Reset_password_with_invalid_token_returns_bad_request()
    {
        using var client = _fixture.Factory.CreateClient();
        var email = $"resetbad{Guid.NewGuid():N}@teamchat.test";
        var (userId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, email);

        var reset = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new { userId, token = "jeton-invalide", password = "Nouveau123!" });

        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
    }

    [Fact]
    public async Task Reset_password_unknown_user_returns_not_found()
    {
        using var client = _fixture.Factory.CreateClient();

        var reset = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new { userId = Guid.NewGuid(), token = "x", password = "Nouveau123!" });

        Assert.Equal(HttpStatusCode.NotFound, reset.StatusCode);
    }

    private async Task<string> GenerateTokenAsync(string email)
    {
        using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var user = await identity.FindByEmailAsync(email, CancellationToken.None);
        Assert.NotNull(user);
        return await identity.GeneratePasswordResetTokenAsync(user!, CancellationToken.None);
    }
}
