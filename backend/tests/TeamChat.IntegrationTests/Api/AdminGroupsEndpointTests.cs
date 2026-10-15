using System.Net;
using System.Net.Http.Json;
using TeamChat.IntegrationTests.Support;

namespace TeamChat.IntegrationTests.Api;

[Collection("api")]
public sealed class AdminGroupsEndpointTests
{
    private readonly ApiCollectionFixture _fixture;

    public AdminGroupsEndpointTests(ApiCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Non_admin_cannot_manage_groups()
    {
        using var client = _fixture.Factory.CreateClient();
        var (_, userToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "grp-nonadmin@teamchat.test");

        var create = await ApiTestHelper.SendAsync(client, HttpMethod.Post, "/api/admin/groups", userToken,
            new { name = "Interdit", memberIds = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);

        var list = await ApiTestHelper.SendAsync(client, HttpMethod.Get, "/api/admin/groups", userToken);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    [Fact]
    public async Task Admin_creates_group_visible_to_members()
    {
        using var client = _fixture.Factory.CreateClient();
        var adminToken = await ApiTestHelper.LoginAsync(client, ApiCollectionFixture.AdminEmail, ApiCollectionFixture.AdminPassword);
        var (memberId, memberToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "grp-member@teamchat.test");
        var (secondId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "grp-second@teamchat.test");

        var create = await ApiTestHelper.SendAsync(client, HttpMethod.Post, "/api/admin/groups", adminToken,
            new { name = "Equipe produit", memberIds = new[] { memberId, secondId } });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var group = await create.Content.ReadFromJsonAsync<GroupDto>();
        Assert.NotNull(group);
        Assert.Equal(3, group!.Members.Count);

        var conversationsResponse = await ApiTestHelper.SendAsync(client, HttpMethod.Get, "/api/conversations", memberToken);
        conversationsResponse.EnsureSuccessStatusCode();
        var conversations = await conversationsResponse.Content.ReadFromJsonAsync<List<ConversationListItemDto>>();
        Assert.Contains(conversations!, c => c.Id == group.Id && c.Type == 1);

        var messages = await ApiTestHelper.SendAsync(client, HttpMethod.Get, $"/api/conversations/{group.Id}/messages", memberToken);
        Assert.Equal(HttpStatusCode.OK, messages.StatusCode);
    }

    [Fact]
    public async Task Removing_member_invalidates_membership_immediately()
    {
        using var client = _fixture.Factory.CreateClient();
        var adminToken = await ApiTestHelper.LoginAsync(client, ApiCollectionFixture.AdminEmail, ApiCollectionFixture.AdminPassword);
        var (memberId, memberToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "grp-leave@teamchat.test");
        var (keepId, keepToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "grp-keep@teamchat.test");

        var create = await ApiTestHelper.SendAsync(client, HttpMethod.Post, "/api/admin/groups", adminToken,
            new { name = "Groupe rotatif", memberIds = new[] { memberId, keepId } });
        create.EnsureSuccessStatusCode();
        var group = await create.Content.ReadFromJsonAsync<GroupDto>();

        var before = await ApiTestHelper.SendAsync(client, HttpMethod.Get, $"/api/conversations/{group!.Id}/messages", memberToken);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        var update = await ApiTestHelper.SendAsync(client, HttpMethod.Put, $"/api/admin/groups/{group.Id}/members", adminToken,
            new { memberIds = new[] { keepId } });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var after = await ApiTestHelper.SendAsync(client, HttpMethod.Get, $"/api/conversations/{group.Id}/messages", memberToken);
        Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);

        var keep = await ApiTestHelper.SendAsync(client, HttpMethod.Get, $"/api/conversations/{group.Id}/messages", keepToken);
        Assert.Equal(HttpStatusCode.OK, keep.StatusCode);
    }

    [Fact]
    public async Task Archived_group_is_inaccessible_and_hidden()
    {
        using var client = _fixture.Factory.CreateClient();
        var adminToken = await ApiTestHelper.LoginAsync(client, ApiCollectionFixture.AdminEmail, ApiCollectionFixture.AdminPassword);
        var (memberId, memberToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "grp-archive@teamchat.test");
        var (secondId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "grp-archive2@teamchat.test");

        var create = await ApiTestHelper.SendAsync(client, HttpMethod.Post, "/api/admin/groups", adminToken,
            new { name = "A archiver", memberIds = new[] { memberId, secondId } });
        create.EnsureSuccessStatusCode();
        var group = await create.Content.ReadFromJsonAsync<GroupDto>();

        var archive = await ApiTestHelper.SendAsync(client, HttpMethod.Delete, $"/api/admin/groups/{group!.Id}", adminToken);
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        var messages = await ApiTestHelper.SendAsync(client, HttpMethod.Get, $"/api/conversations/{group.Id}/messages", memberToken);
        Assert.Equal(HttpStatusCode.Forbidden, messages.StatusCode);

        var conversationsResponse = await ApiTestHelper.SendAsync(client, HttpMethod.Get, "/api/conversations", memberToken);
        conversationsResponse.EnsureSuccessStatusCode();
        var conversations = await conversationsResponse.Content.ReadFromJsonAsync<List<ConversationListItemDto>>();
        Assert.DoesNotContain(conversations!, c => c.Id == group.Id);
    }

    [Fact]
    public async Task Admin_can_get_detail_and_rename_group()
    {
        using var client = _fixture.Factory.CreateClient();
        var adminToken = await ApiTestHelper.LoginAsync(client, ApiCollectionFixture.AdminEmail, ApiCollectionFixture.AdminPassword);
        var (memberId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "grp-detail@teamchat.test");
        var (secondId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "grp-detail2@teamchat.test");

        var create = await ApiTestHelper.SendAsync(client, HttpMethod.Post, "/api/admin/groups", adminToken,
            new { name = "Nom initial", memberIds = new[] { memberId, secondId } });
        create.EnsureSuccessStatusCode();
        var group = await create.Content.ReadFromJsonAsync<GroupDto>();

        var detail = await ApiTestHelper.SendAsync(client, HttpMethod.Get, $"/api/admin/groups/{group!.Id}", adminToken);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var loaded = await detail.Content.ReadFromJsonAsync<GroupDto>();
        Assert.Equal(3, loaded!.Members.Count);

        var missing = await ApiTestHelper.SendAsync(client, HttpMethod.Get, $"/api/admin/groups/{Guid.NewGuid()}", adminToken);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var rename = await ApiTestHelper.SendAsync(client, HttpMethod.Put, $"/api/admin/groups/{group.Id}", adminToken,
            new { name = "Nom renomme" });
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        var renamed = await rename.Content.ReadFromJsonAsync<GroupDto>();
        Assert.Equal("Nom renomme", renamed!.Name);

        var invalid = await ApiTestHelper.SendAsync(client, HttpMethod.Put, $"/api/admin/groups/{group.Id}", adminToken,
            new { name = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Unarchive_restores_group_access()
    {
        using var client = _fixture.Factory.CreateClient();
        var adminToken = await ApiTestHelper.LoginAsync(client, ApiCollectionFixture.AdminEmail, ApiCollectionFixture.AdminPassword);
        var (memberId, memberToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "grp-unarchive@teamchat.test");
        var (secondId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "grp-unarchive2@teamchat.test");

        var create = await ApiTestHelper.SendAsync(client, HttpMethod.Post, "/api/admin/groups", adminToken,
            new { name = "A desarchiver", memberIds = new[] { memberId, secondId } });
        create.EnsureSuccessStatusCode();
        var group = await create.Content.ReadFromJsonAsync<GroupDto>();

        var archive = await ApiTestHelper.SendAsync(client, HttpMethod.Delete, $"/api/admin/groups/{group!.Id}", adminToken);
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        var blocked = await ApiTestHelper.SendAsync(client, HttpMethod.Get, $"/api/conversations/{group.Id}/messages", memberToken);
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);

        var unarchive = await ApiTestHelper.SendAsync(client, HttpMethod.Post, $"/api/admin/groups/{group.Id}/unarchive", adminToken);
        Assert.Equal(HttpStatusCode.NoContent, unarchive.StatusCode);

        var restored = await ApiTestHelper.SendAsync(client, HttpMethod.Get, $"/api/conversations/{group.Id}/messages", memberToken);
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
    }

    [Fact]
    public async Task Permanent_delete_removes_group()
    {
        using var client = _fixture.Factory.CreateClient();
        var adminToken = await ApiTestHelper.LoginAsync(client, ApiCollectionFixture.AdminEmail, ApiCollectionFixture.AdminPassword);
        var (memberId, memberToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "grp-delete@teamchat.test");
        var (secondId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "grp-delete2@teamchat.test");

        var create = await ApiTestHelper.SendAsync(client, HttpMethod.Post, "/api/admin/groups", adminToken,
            new { name = "A supprimer", memberIds = new[] { memberId, secondId } });
        create.EnsureSuccessStatusCode();
        var group = await create.Content.ReadFromJsonAsync<GroupDto>();

        var delete = await ApiTestHelper.SendAsync(client, HttpMethod.Delete, $"/api/admin/groups/{group!.Id}/permanent", adminToken);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var detail = await ApiTestHelper.SendAsync(client, HttpMethod.Get, $"/api/admin/groups/{group.Id}", adminToken);
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);

        var conversationsResponse = await ApiTestHelper.SendAsync(client, HttpMethod.Get, "/api/conversations", memberToken);
        conversationsResponse.EnsureSuccessStatusCode();
        var conversations = await conversationsResponse.Content.ReadFromJsonAsync<List<ConversationListItemDto>>();
        Assert.DoesNotContain(conversations!, c => c.Id == group.Id);
    }
}
