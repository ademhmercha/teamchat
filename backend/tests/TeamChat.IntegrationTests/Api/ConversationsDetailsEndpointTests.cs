using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TeamChat.Application.Abstractions;
using TeamChat.Domain.Entities;
using TeamChat.IntegrationTests.Support;

namespace TeamChat.IntegrationTests.Api;

[Collection("api")]
public sealed class ConversationsDetailsEndpointTests
{
    private readonly ApiCollectionFixture _fixture;

    public ConversationsDetailsEndpointTests(ApiCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Get_detail_returns_participants_for_member()
    {
        using var client = _fixture.Factory.CreateClient();
        var (_, tokenA) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "detail-a@teamchat.test");
        var (otherId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "detail-b@teamchat.test");

        var conversationId = await ApiTestHelper.CreateDirectAsync(client, tokenA, otherId);

        var response = await ApiTestHelper.SendAsync(client, HttpMethod.Get, $"/api/conversations/{conversationId}", tokenA);
        response.EnsureSuccessStatusCode();

        var detail = await response.Content.ReadFromJsonAsync<ConversationDetailDto>();
        Assert.NotNull(detail);
        Assert.Equal(conversationId, detail!.Id);
        Assert.Equal(2, detail.Participants.Count);
        Assert.False(detail.Archived);
    }

    [Fact]
    public async Task Get_detail_when_not_member_returns_403()
    {
        using var client = _fixture.Factory.CreateClient();
        var (_, tokenA) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "detail-c@teamchat.test");
        var (otherId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "detail-d@teamchat.test");
        var (_, strangerToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "detail-e@teamchat.test");

        var conversationId = await ApiTestHelper.CreateDirectAsync(client, tokenA, otherId);

        var response = await ApiTestHelper.SendAsync(client, HttpMethod.Get, $"/api/conversations/{conversationId}", strangerToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_members_returns_participants()
    {
        using var client = _fixture.Factory.CreateClient();
        var (_, tokenA) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "members-a@teamchat.test");
        var (otherId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "members-b@teamchat.test");

        var conversationId = await ApiTestHelper.CreateDirectAsync(client, tokenA, otherId);

        var response = await ApiTestHelper.SendAsync(client, HttpMethod.Get, $"/api/conversations/{conversationId}/members", tokenA);
        response.EnsureSuccessStatusCode();

        var members = await response.Content.ReadFromJsonAsync<List<ConversationParticipantDto>>();
        Assert.NotNull(members);
        Assert.Equal(2, members!.Count);
        Assert.Contains(members, m => m.UserId == otherId);
    }

    [Fact]
    public async Task Archive_then_unarchive_updates_flag_for_current_user_only()
    {
        using var client = _fixture.Factory.CreateClient();
        var (_, tokenA) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "arch-a@teamchat.test");
        var (otherId, otherToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "arch-b@teamchat.test");

        var conversationId = await ApiTestHelper.CreateDirectAsync(client, tokenA, otherId);

        var archive = await ApiTestHelper.SendAsync(client, HttpMethod.Patch, $"/api/conversations/{conversationId}", tokenA, new { archived = true });
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        var detail = await (await ApiTestHelper.SendAsync(client, HttpMethod.Get, $"/api/conversations/{conversationId}", tokenA))
            .Content.ReadFromJsonAsync<ConversationDetailDto>();
        Assert.True(detail!.Archived);

        var otherDetail = await (await ApiTestHelper.SendAsync(client, HttpMethod.Get, $"/api/conversations/{conversationId}", otherToken))
            .Content.ReadFromJsonAsync<ConversationDetailDto>();
        Assert.False(otherDetail!.Archived);

        var unarchive = await ApiTestHelper.SendAsync(client, HttpMethod.Patch, $"/api/conversations/{conversationId}", tokenA, new { archived = false });
        Assert.Equal(HttpStatusCode.NoContent, unarchive.StatusCode);

        var listed = await (await ApiTestHelper.SendAsync(client, HttpMethod.Get, "/api/conversations", tokenA))
            .Content.ReadFromJsonAsync<List<ConversationListItemDto>>();
        Assert.False(listed!.Single(c => c.Id == conversationId).Archived);
    }

    [Fact]
    public async Task Leave_group_removes_membership()
    {
        using var client = _fixture.Factory.CreateClient();
        var adminToken = await ApiTestHelper.LoginAsync(client, ApiCollectionFixture.AdminEmail, ApiCollectionFixture.AdminPassword);
        var (memberId, memberToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "leave-member@teamchat.test");
        var (keepId, keepToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "leave-keep@teamchat.test");

        var create = await ApiTestHelper.SendAsync(client, HttpMethod.Post, "/api/admin/groups", adminToken,
            new { name = "Groupe depart", memberIds = new[] { memberId, keepId } });
        create.EnsureSuccessStatusCode();
        var group = await create.Content.ReadFromJsonAsync<GroupDto>();

        var leave = await ApiTestHelper.SendAsync(client, HttpMethod.Post, $"/api/conversations/{group!.Id}/leave", memberToken);
        Assert.Equal(HttpStatusCode.NoContent, leave.StatusCode);

        var after = await ApiTestHelper.SendAsync(client, HttpMethod.Get, $"/api/conversations/{group.Id}/messages", memberToken);
        Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);

        var keep = await ApiTestHelper.SendAsync(client, HttpMethod.Get, $"/api/conversations/{group.Id}/messages", keepToken);
        Assert.Equal(HttpStatusCode.OK, keep.StatusCode);
    }

    [Fact]
    public async Task Leave_direct_conversation_returns_400()
    {
        using var client = _fixture.Factory.CreateClient();
        var (_, tokenA) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "leave-direct-a@teamchat.test");
        var (otherId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "leave-direct-b@teamchat.test");

        var conversationId = await ApiTestHelper.CreateDirectAsync(client, tokenA, otherId);

        var leave = await ApiTestHelper.SendAsync(client, HttpMethod.Post, $"/api/conversations/{conversationId}/leave", tokenA);
        Assert.Equal(HttpStatusCode.BadRequest, leave.StatusCode);
    }

    [Fact]
    public async Task Search_messages_matches_content()
    {
        using var client = _fixture.Factory.CreateClient();
        var (myId, token) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "search-a@teamchat.test");
        var (otherId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "search-b@teamchat.test");

        var conversationId = await ApiTestHelper.CreateDirectAsync(client, token, otherId);

        await ApiTestHelper.RunWithTenantAsync(_fixture.Factory, TeamChat.Domain.Constants.TenantDefaults.DefaultTenantId, async provider =>
        {
            var conversations = provider.GetRequiredService<IConversationService>();
            await conversations.SendMessageAsync(myId, conversationId, "Reunion budget lundi", CancellationToken.None);
            await conversations.SendMessageAsync(otherId, conversationId, "Note sur le budget trimestriel", CancellationToken.None);
            await conversations.SendMessageAsync(myId, conversationId, "Autre sujet", CancellationToken.None);
        });

        var response = await ApiTestHelper.SendAsync(client, HttpMethod.Get, $"/api/conversations/{conversationId}/messages/search?q=budget", token);
        response.EnsureSuccessStatusCode();

        var results = await response.Content.ReadFromJsonAsync<MessageHistoryResult>();
        Assert.NotNull(results);
        Assert.Equal(2, results!.Items.Count);
        Assert.All(results.Items, m => Assert.Contains("budget", m.Content!, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Shared_attachments_lists_uploaded_files()
    {
        using var client = _fixture.Factory.CreateClient();
        var (myId, token) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "files-a@teamchat.test");
        var (otherId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "files-b@teamchat.test");

        var conversationId = await ApiTestHelper.CreateDirectAsync(client, token, otherId);

        await ApiTestHelper.RunWithTenantAsync(_fixture.Factory, TeamChat.Domain.Constants.TenantDefaults.DefaultTenantId, async provider =>
        {
            var attachments = provider.GetRequiredService<IAttachmentQueries>();
            await attachments.PersistAttachmentMessageAsync(
                conversationId,
                myId,
                "storage-key-1",
                "rapport.pdf",
                "application/pdf",
                2048,
                null,
                MessageType.File,
                DateTimeOffset.UtcNow,
                CancellationToken.None);
        });

        var response = await ApiTestHelper.SendAsync(client, HttpMethod.Get, $"/api/conversations/{conversationId}/attachments", token);
        response.EnsureSuccessStatusCode();

        var files = await response.Content.ReadFromJsonAsync<List<SharedAttachmentDto>>();
        Assert.NotNull(files);
        var file = Assert.Single(files!);
        Assert.Equal("rapport.pdf", file.FileName);
        Assert.Equal(2048, file.SizeBytes);
        Assert.Equal(myId, file.SenderId);
    }

    [Fact]
    public async Task Search_and_files_when_not_member_returns_403()
    {
        using var client = _fixture.Factory.CreateClient();
        var (_, tokenA) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "guard-a@teamchat.test");
        var (otherId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "guard-b@teamchat.test");
        var (_, strangerToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "guard-c@teamchat.test");

        var conversationId = await ApiTestHelper.CreateDirectAsync(client, tokenA, otherId);

        var search = await ApiTestHelper.SendAsync(client, HttpMethod.Get, $"/api/conversations/{conversationId}/messages/search?q=x", strangerToken);
        Assert.Equal(HttpStatusCode.Forbidden, search.StatusCode);

        var files = await ApiTestHelper.SendAsync(client, HttpMethod.Get, $"/api/conversations/{conversationId}/attachments", strangerToken);
        Assert.Equal(HttpStatusCode.Forbidden, files.StatusCode);
    }
}
