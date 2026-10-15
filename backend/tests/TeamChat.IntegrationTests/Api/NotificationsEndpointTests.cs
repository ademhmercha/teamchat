using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using TeamChat.IntegrationTests.Support;

namespace TeamChat.IntegrationTests.Api;

[Collection("api")]
public sealed class NotificationsEndpointTests
{
    private readonly ApiCollectionFixture _fixture;

    public NotificationsEndpointTests(ApiCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Fresh_user_has_no_notifications()
    {
        using var client = _fixture.Factory.CreateClient();
        var (_, token) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "notif-fresh@teamchat.test");

        var response = await ApiTestHelper.SendAsync(client, HttpMethod.Get, "/api/notifications", token);
        response.EnsureSuccessStatusCode();
        var list = await response.Content.ReadFromJsonAsync<NotificationListDto>();

        Assert.NotNull(list);
        Assert.Empty(list!.Items);
        Assert.Equal(0, list.UnreadCount);
    }

    [Fact]
    public async Task Notifications_require_authentication()
    {
        using var client = _fixture.Factory.CreateClient();
        var response = await client.GetAsync("/api/notifications");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Added_group_member_receives_notification()
    {
        using var client = _fixture.Factory.CreateClient();
        var adminToken = await ApiTestHelper.LoginAsync(client, ApiCollectionFixture.AdminEmail, ApiCollectionFixture.AdminPassword);
        var (memberId, memberToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "notif-group-member@teamchat.test");
        var (secondId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "notif-group-second@teamchat.test");

        var create = await ApiTestHelper.SendAsync(client, HttpMethod.Post, "/api/admin/groups", adminToken,
            new { name = "Notif equipe", memberIds = new[] { memberId, secondId } });
        create.EnsureSuccessStatusCode();
        var group = await create.Content.ReadFromJsonAsync<GroupDto>();

        var response = await ApiTestHelper.SendAsync(client, HttpMethod.Get, "/api/notifications", memberToken);
        response.EnsureSuccessStatusCode();
        var list = await response.Content.ReadFromJsonAsync<NotificationListDto>();

        var notification = Assert.Single(list!.Items);
        Assert.Equal(0, notification.Type);
        Assert.Equal(group!.Id, notification.ConversationId);
        Assert.False(notification.Read);
        Assert.Equal(1, list.UnreadCount);
    }

    [Fact]
    public async Task Mark_read_updates_unread_count()
    {
        using var client = _fixture.Factory.CreateClient();
        var adminToken = await ApiTestHelper.LoginAsync(client, ApiCollectionFixture.AdminEmail, ApiCollectionFixture.AdminPassword);
        var (memberId, memberToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "notif-read@teamchat.test");
        var (secondId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "notif-read-second@teamchat.test");

        var create = await ApiTestHelper.SendAsync(client, HttpMethod.Post, "/api/admin/groups", adminToken,
            new { name = "Notif lu", memberIds = new[] { memberId, secondId } });
        create.EnsureSuccessStatusCode();

        var before = await ApiTestHelper.SendAsync(client, HttpMethod.Get, "/api/notifications", memberToken);
        var beforeList = await before.Content.ReadFromJsonAsync<NotificationListDto>();
        var target = Assert.Single(beforeList!.Items);

        var mark = await ApiTestHelper.SendAsync(client, HttpMethod.Post, $"/api/notifications/{target.Id}/read", memberToken);
        Assert.Equal(HttpStatusCode.NoContent, mark.StatusCode);

        var after = await ApiTestHelper.SendAsync(client, HttpMethod.Get, "/api/notifications", memberToken);
        var afterList = await after.Content.ReadFromJsonAsync<NotificationListDto>();
        Assert.Equal(0, afterList!.UnreadCount);
        Assert.True(Assert.Single(afterList.Items).Read);
    }

    [Fact]
    public async Task Unread_only_filter_and_mark_all_read()
    {
        using var client = _fixture.Factory.CreateClient();
        var adminToken = await ApiTestHelper.LoginAsync(client, ApiCollectionFixture.AdminEmail, ApiCollectionFixture.AdminPassword);
        var (memberId, memberToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "notif-all@teamchat.test");
        var (secondId, _) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "notif-all-two@teamchat.test");

        var first = await ApiTestHelper.SendAsync(client, HttpMethod.Post, "/api/admin/groups", adminToken,
            new { name = "Notif A", memberIds = new[] { memberId, secondId } });
        first.EnsureSuccessStatusCode();
        var second = await ApiTestHelper.SendAsync(client, HttpMethod.Post, "/api/admin/groups", adminToken,
            new { name = "Notif B", memberIds = new[] { memberId, secondId } });
        second.EnsureSuccessStatusCode();

        var all = await ApiTestHelper.SendAsync(client, HttpMethod.Get, "/api/notifications", memberToken);
        var allList = await all.Content.ReadFromJsonAsync<NotificationListDto>();
        Assert.Equal(2, allList!.UnreadCount);

        var unread = await ApiTestHelper.SendAsync(client, HttpMethod.Get, "/api/notifications?unreadOnly=true", memberToken);
        var unreadList = await unread.Content.ReadFromJsonAsync<NotificationListDto>();
        Assert.Equal(2, unreadList!.Items.Count);

        var markAll = await ApiTestHelper.SendAsync(client, HttpMethod.Post, "/api/notifications/read-all", memberToken);
        Assert.Equal(HttpStatusCode.NoContent, markAll.StatusCode);

        var afterUnread = await ApiTestHelper.SendAsync(client, HttpMethod.Get, "/api/notifications?unreadOnly=true", memberToken);
        var afterList = await afterUnread.Content.ReadFromJsonAsync<NotificationListDto>();
        Assert.Empty(afterList!.Items);
        Assert.Equal(0, afterList.UnreadCount);
    }

    [Fact]
    public async Task Marking_unknown_notification_returns_not_found()
    {
        using var client = _fixture.Factory.CreateClient();
        var (_, token) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "notif-404@teamchat.test");

        var response = await ApiTestHelper.SendAsync(client, HttpMethod.Post, $"/api/notifications/{Guid.NewGuid()}/read", token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Missed_call_creates_notification_for_callee()
    {
        using var client = _fixture.Factory.CreateClient();
        var (callerId, callerToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "notif-call-caller@teamchat.test");
        var (calleeId, calleeToken) = await ApiTestHelper.ProvisionUserAndLoginAsync(_fixture, client, "notif-call-callee@teamchat.test");

        await using var calleeConnection = CreateConnection(calleeToken);
        await using var connection = CreateConnection(callerToken);
        await calleeConnection.StartAsync();
        await connection.StartAsync();

        var session = await connection.InvokeAsync<CallSessionDto>("CallUser", calleeId, 0);
        await connection.InvokeAsync("EndCall", session.CallId);

        var response = await ApiTestHelper.SendAsync(client, HttpMethod.Get, "/api/notifications", calleeToken);
        response.EnsureSuccessStatusCode();
        var list = await response.Content.ReadFromJsonAsync<NotificationListDto>();

        var notification = Assert.Single(list!.Items);
        Assert.Equal(1, notification.Type);
        Assert.Equal(callerId, notification.ActorUserId);
    }

    private HubConnection CreateConnection(string accessToken)
    {
        var handler = _fixture.Factory.Server.CreateHandler();
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
}
