using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Auth.Dtos;
using TeamChat.Domain.Entities;

namespace TeamChat.IntegrationTests.Support;

/// <summary>Helpers partages par les tests d'integration (comptes, connexions, requetes).</summary>
public static class ApiTestHelper
{
    public static Task<(Guid UserId, string AccessToken)> ProvisionUserAndLoginAsync(
        ApiCollectionFixture fixture,
        HttpClient client,
        string email)
        => ProvisionUserAndLoginAsync(fixture.Factory, client, email);

    public static async Task<(Guid UserId, string AccessToken)> ProvisionUserAndLoginAsync(
        TeamChatApiFactory factory,
        HttpClient client,
        string email)
    {
        Guid userId;
        using (var scope = factory.Services.CreateAsyncScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();

            var user = await identity.FindByEmailAsync(email, CancellationToken.None);
            if (user is null)
            {
                var entity = new AppUser
                {
                    UserName = email,
                    Email = email,
                    DisplayName = "Testeur " + email.Split('@')[0],
                    IsActive = true,
                    EmailConfirmed = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                var created = await identity.CreateAsync(entity, CancellationToken.None);
                Assert.True(created.Succeeded);
                user = entity;
            }

            var token = await identity.GeneratePasswordResetTokenAsync(user, CancellationToken.None);
            var reset = await identity.ResetPasswordAsync(user, token, "Password123!", CancellationToken.None);
            Assert.True(reset.Succeeded);
            userId = user.Id;
        }

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return (userId, auth!.AccessToken);
    }

    public static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.AccessToken;
    }

    public static async Task<Guid> CreateDirectAsync(HttpClient client, string accessToken, Guid otherId)
    {
        var response = await SendAsync(client, HttpMethod.Post, "/api/conversations/direct", accessToken, new { userId = otherId });
        response.EnsureSuccessStatusCode();
        var conversation = await response.Content.ReadFromJsonAsync<ConversationDto>();
        return conversation!.Id;
    }

    public static HubConnection CreateConnection(ApiCollectionFixture fixture, string accessToken)
        => CreateConnection(fixture.Factory, accessToken);

    public static HubConnection CreateConnection(TeamChatApiFactory factory, string accessToken)
    {
        var handler = factory.Server.CreateHandler();
        return new HubConnectionBuilder()
            .WithUrl(new Uri("http://localhost/hubs/chat"), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                options.HttpMessageHandlerFactory = _ => handler;
                options.Transports = HttpTransportType.LongPolling;
            })
            .WithAutomaticReconnect()
            .Build();
    }

    public static Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        string accessToken,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, url)
        {
            Content = body is null ? null : JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }
}
