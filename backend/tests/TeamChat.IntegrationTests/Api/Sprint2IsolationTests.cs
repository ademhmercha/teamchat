using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TeamChat.Application.Auth.Dtos;
using TeamChat.IntegrationTests.Support;

namespace TeamChat.IntegrationTests.Api;

/// <summary>
/// Sprint 2 : cloture stricte des donnees entre societes sur les parcours
/// sensibles (conversation directe, profil, avatar) et administration
/// plateforme reservee au super-admin (liste, renommage, activation).
/// </summary>
[Collection("api")]
public sealed class Sprint2IsolationTests
{
    private const string Password = "OwnerPass123!";

    private readonly ApiCollectionFixture _fixture;

    public Sprint2IsolationTests(ApiCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Direct_conversation_with_user_from_another_company_is_rejected()
    {
        using var client = _fixture.Factory.CreateClient();
        var ownerA = await RegisterCompanyAsync(client, "Direct Alpha", "owner-direct-alpha@teamchat.test");
        var ownerB = await RegisterCompanyAsync(client, "Direct Beta", "owner-direct-beta@teamchat.test");

        var response = await ApiTestHelper.SendAsync(
            client, HttpMethod.Post, "/api/conversations/direct", ownerA.Token,
            new { userId = ownerB.UserId });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Profile_of_user_from_another_company_is_not_visible()
    {
        using var client = _fixture.Factory.CreateClient();
        var ownerA = await RegisterCompanyAsync(client, "Profile Alpha", "owner-profile-alpha@teamchat.test");
        var ownerB = await RegisterCompanyAsync(client, "Profile Beta", "owner-profile-beta@teamchat.test");

        var other = await ApiTestHelper.SendAsync(
            client, HttpMethod.Get, $"/api/users/{ownerB.UserId}", ownerA.Token);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);

        var self = await ApiTestHelper.SendAsync(
            client, HttpMethod.Get, $"/api/users/{ownerA.UserId}", ownerA.Token);
        Assert.Equal(HttpStatusCode.OK, self.StatusCode);
    }

    [Fact]
    public async Task Avatar_of_another_company_requires_authentication()
    {
        using var client = _fixture.Factory.CreateClient();
        var ownerA = await RegisterCompanyAsync(client, "Avatar Alpha", "owner-avatar-alpha@teamchat.test");
        var ownerB = await RegisterCompanyAsync(client, "Avatar Beta", "owner-avatar-beta@teamchat.test");

        var anonymous = await client.GetAsync($"/api/users/{ownerB.UserId}/avatar");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var crossTenant = await ApiTestHelper.SendAsync(
            client, HttpMethod.Get, $"/api/users/{ownerB.UserId}/avatar", ownerA.Token);
        Assert.Equal(HttpStatusCode.NotFound, crossTenant.StatusCode);
    }

    [Fact]
    public async Task Platform_endpoints_require_super_admin()
    {
        using var client = _fixture.Factory.CreateClient();
        var owner = await RegisterCompanyAsync(client, "Platform Guard", "owner-platform-guard@teamchat.test");

        var anonymous = await client.GetAsync("/api/platform/tenants");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var forbidden = await ApiTestHelper.SendAsync(
            client, HttpMethod.Get, "/api/platform/tenants", owner.Token);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Super_admin_can_list_and_rename_a_company()
    {
        using var client = _fixture.Factory.CreateClient();
        var owner = await RegisterCompanyAsync(client, "Rename Co", "owner-rename@teamchat.test");
        var superToken = await ApiTestHelper.LoginAsync(
            client, ApiCollectionFixture.SuperAdminEmail, ApiCollectionFixture.SuperAdminPassword);

        var list = await SendJsonAsync<IReadOnlyList<PlatformTenantDto>>(
            client, HttpMethod.Get, "/api/platform/tenants", superToken);
        var tenant = list.Single(t => t.Id == owner.TenantId);
        Assert.Equal("Rename Co", tenant.Name);
        Assert.True(tenant.UserCount >= 1);

        var renamed = await SendJsonAsync<PlatformTenantDto>(
            client, HttpMethod.Patch, $"/api/platform/tenants/{owner.TenantId}", superToken,
            new { name = "Renamed Co" });
        Assert.Equal("Renamed Co", renamed.Name);

        var detail = await SendJsonAsync<PlatformTenantDto>(
            client, HttpMethod.Get, $"/api/platform/tenants/{owner.TenantId}", superToken);
        Assert.Equal("Renamed Co", detail.Name);
    }

    [Fact]
    public async Task Deactivated_company_blocks_member_login_until_reactivated()
    {
        using var client = _fixture.Factory.CreateClient();
        var email = "owner-deactivate@teamchat.test";
        var owner = await RegisterCompanyAsync(client, "Deact Co", email);
        var superToken = await ApiTestHelper.LoginAsync(
            client, ApiCollectionFixture.SuperAdminEmail, ApiCollectionFixture.SuperAdminPassword);

        var deactivated = await SendJsonAsync<PlatformTenantDto>(
            client, HttpMethod.Patch, $"/api/platform/tenants/{owner.TenantId}", superToken,
            new { isActive = false });
        Assert.False(deactivated.IsActive);

        var blocked = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.Unauthorized, blocked.StatusCode);

        var reactivated = await SendJsonAsync<PlatformTenantDto>(
            client, HttpMethod.Patch, $"/api/platform/tenants/{owner.TenantId}", superToken,
            new { isActive = true });
        Assert.True(reactivated.IsActive);

        var allowed = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    private async Task<CompanyOwner> RegisterCompanyAsync(HttpClient client, string company, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register-company",
            new RegisterCompanyRequest(company, "Owner " + company, email, Password));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return new CompanyOwner(auth!.AccessToken, auth.User!.Id, auth.User!.TenantId!.Value);
    }

    private static async Task<T> SendJsonAsync<T>(
        HttpClient client, HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private sealed record CompanyOwner(string Token, Guid UserId, Guid TenantId);
}
