using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Auth.Dtos;
using TeamChat.IntegrationTests.Support;

namespace TeamChat.IntegrationTests.Api;

[Collection("api")]
public sealed class ConversationsEndpointTests
{
    private readonly ApiCollectionFixture _fixture;

    public ConversationsEndpointTests(ApiCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Create_direct_conversation_returns_both_participants()
    {
        using var client = _fixture.Factory.CreateClient();
        var (otherId, _) = await ProvisionUserAndLoginAsync(client, "destinataire@teamchat.test");
        var (_, token) = await ProvisionUserAndLoginAsync(client, "expediteur@teamchat.test");

        var response = await CreateDirectAsync(client, token, otherId);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"STATUS={(int)response.StatusCode} BODY={body}");
        var conversation = await response.Content.ReadFromJsonAsync<ConversationDto>();
        Assert.NotNull(conversation);
        Assert.Equal(2, conversation!.Participants.Count);
        Assert.Contains(conversation.Participants, p => p.UserId == otherId);
    }

    [Fact]
    public async Task Create_direct_conversation_is_idempotent()
    {
        using var client = _fixture.Factory.CreateClient();
        var (_, tokenA) = await ProvisionUserAndLoginAsync(client, "idempotentA@teamchat.test");
        var (otherId, _) = await ProvisionUserAndLoginAsync(client, "idempotentB@teamchat.test");

        var first = await CreateDirectAsync(client, tokenA, otherId);
        first.EnsureSuccessStatusCode();
        var firstConversation = await first.Content.ReadFromJsonAsync<ConversationDto>();

        var second = await CreateDirectAsync(client, tokenA, otherId);
        second.EnsureSuccessStatusCode();
        var secondConversation = await second.Content.ReadFromJsonAsync<ConversationDto>();

        Assert.NotNull(firstConversation);
        Assert.NotNull(secondConversation);
        Assert.Equal(firstConversation!.Id, secondConversation!.Id);
    }

    [Fact]
    public async Task Create_direct_conversation_with_self_returns_400()
    {
        using var client = _fixture.Factory.CreateClient();
        var (myId, token) = await ProvisionUserAndLoginAsync(client, "soimeme@teamchat.test");

        var response = await CreateDirectAsync(client, token, myId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Direct_conversation_requires_authentication()
    {
        using var client = _fixture.Factory.CreateClient();

        var response = await CreateDirectAsync(client, null, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_messages_returns_latest_first_with_cursor()
    {
        using var client = _fixture.Factory.CreateClient();
        var (firstUserId, firstToken) = await ProvisionUserAndLoginAsync(client, "historiqueA@teamchat.test");
        var (secondUserId, secondToken) = await ProvisionUserAndLoginAsync(client, "historiqueB@teamchat.test");

        var conversation = await CreateDirectAsync(client, firstToken, secondUserId);
        conversation.EnsureSuccessStatusCode();
        var conversationId = (await conversation.Content.ReadFromJsonAsync<ConversationDto>())!.Id;

        await SeedMessagesAsync(conversationId, firstUserId);
        await SeedMessagesAsync(conversationId, secondUserId);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/conversations/{conversationId}/messages?limit=3");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", firstToken);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var history = await response.Content.ReadFromJsonAsync<MessageHistoryResult>();
        Assert.NotNull(history);
        Assert.Equal(3, history!.Items.Count);
        Assert.True(history.HasMore);
        Assert.NotNull(history.NextCursor);

        var nextRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/conversations/{conversationId}/messages?limit=3&before={history.NextCursor!.Sequence}");
        nextRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", firstToken);
        var nextResponse = await client.SendAsync(nextRequest);
        nextResponse.EnsureSuccessStatusCode();
        var rest = await nextResponse.Content.ReadFromJsonAsync<MessageHistoryResult>();

        Assert.NotNull(rest);
        Assert.True(rest!.Items.Count >= 2);

        var allSequences = history.Items.Concat(rest.Items).Select(m => m.Sequence).ToList();
        Assert.Equal(allSequences.Count, allSequences.Distinct().Count());
        Assert.Equal(
            allSequences.OrderByDescending(s => s),
            allSequences);
        Assert.All(rest.Items, m => Assert.True(m.Sequence < history.NextCursor!.Sequence));
    }

    [Fact]
    public async Task Get_messages_when_not_member_returns_403()
    {
        using var client = _fixture.Factory.CreateClient();
        var (memberAId, tokenA) = await ProvisionUserAndLoginAsync(client, "membreA@teamchat.test");
        var (memberBId, _) = await ProvisionUserAndLoginAsync(client, "membreB@teamchat.test");
        var (_, strangerToken) = await ProvisionUserAndLoginAsync(client, "etranger@teamchat.test");

        var conversation = await CreateDirectAsync(client, tokenA, memberBId);
        conversation.EnsureSuccessStatusCode();
        var conversationId = (await conversation.Content.ReadFromJsonAsync<ConversationDto>())!.Id;

        Assert.NotEqual(memberAId, memberBId);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/conversations/{conversationId}/messages");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", strangerToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_my_conversations_lists_direct_exchanges_with_last_message()
    {
        using var client = _fixture.Factory.CreateClient();
        var (myUserId, token) = await ProvisionUserAndLoginAsync(client, "liste@teamchat.test");
        var (otherId, _) = await ProvisionUserAndLoginAsync(client, "listeAutre@teamchat.test");

        var conversation = await CreateDirectAsync(client, token, otherId);
        conversation.EnsureSuccessStatusCode();
        var conversationId = (await conversation.Content.ReadFromJsonAsync<ConversationDto>())!.Id;

        await SeedMessagesAsync(conversationId, myUserId);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/conversations");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var items = await response.Content.ReadFromJsonAsync<IReadOnlyList<ConversationListItemDto>>();
        Assert.NotNull(items);
        var item = items!.SingleOrDefault(i => i.Id == conversationId);
        Assert.NotNull(item);
        Assert.Equal(otherId, item!.OtherUserId);
        Assert.NotNull(item.LastMessageAt);
    }

    private async Task<(Guid UserId, string AccessToken)> ProvisionUserAndLoginAsync(HttpClient client, string email)
    {
        Guid userId;
        using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();

            var user = await identity.FindByEmailAsync(email, CancellationToken.None);
            if (user is null)
            {
                var userEntity = new TeamChat.Domain.Entities.AppUser
                {
                    UserName = email,
                    Email = email,
                    DisplayName = "Conversation Tester",
                    IsActive = true,
                    EmailConfirmed = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                var created = await identity.CreateAsync(userEntity, CancellationToken.None);
                Assert.True(created.Succeeded);
                user = userEntity;
            }

            var token = await identity.GeneratePasswordResetTokenAsync(user, CancellationToken.None);
            var reset = await identity.ResetPasswordAsync(user, token, "Password123!", CancellationToken.None);
            Assert.True(reset.Succeeded);
            userId = user.Id;
        }

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "Password123!"));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return (userId, auth!.AccessToken);
    }

    private static Task<HttpResponseMessage> CreateDirectAsync(HttpClient client, string? accessToken, Guid otherUserId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/conversations/direct")
        {
            Content = JsonContent.Create(new { userId = otherUserId }),
        };
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return client.SendAsync(request);
    }

    private async Task SeedMessagesAsync(Guid conversationId, Guid senderUserId)
    {
        using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var conversations = scope.ServiceProvider.GetRequiredService<IConversationService>();

        for (var i = 0; i < 5; i++)
        {
            await conversations.SendMessageAsync(
                senderUserId,
                conversationId,
                $"Message de test {i}",
                CancellationToken.None);
        }
    }
}