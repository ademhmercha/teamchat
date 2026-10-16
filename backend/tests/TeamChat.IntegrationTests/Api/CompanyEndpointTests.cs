using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Auth.Dtos;
using TeamChat.Domain.Constants;
using TeamChat.IntegrationTests.Support;
using CreateUserRequest = TeamChat.Application.Users.Dtos.CreateUserRequest;

namespace TeamChat.IntegrationTests.Api;

/// <summary>
/// Administration de la societe : PATCH /api/company reserve a l'Owner,
/// cloisonne a sa propre societe.
/// </summary>
[Collection("api")]
public sealed class CompanyEndpointTests
{
    private const string Password = "OwnerPass123!";
    private const string MemberPassword = "MemberPass123!";

    private readonly ApiCollectionFixture _fixture;

    public CompanyEndpointTests(ApiCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Owner_can_rename_company_and_new_name_is_visible_on_login()
    {
        using var client = _fixture.Factory.CreateClient();
        var email = "owner-rename-endpoint@teamchat.test";
        var owner = await RegisterCompanyAsync(client, "Avant Rename", email);

        var response = await ApiTestHelper.SendAsync(
            client, HttpMethod.Patch, "/api/company", owner.Token, new { name = "Apres Rename" });
        response.EnsureSuccessStatusCode();
        var tenant = await response.Content.ReadFromJsonAsync<TenantDto>();
        Assert.Equal("Apres Rename", tenant!.Name);
        Assert.Equal(owner.TenantId, tenant.Id);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal("Apres Rename", auth!.User!.TenantName);
    }

    [Fact]
    public async Task Rename_only_affects_the_owner_company()
    {
        using var client = _fixture.Factory.CreateClient();
        var ownerA = await RegisterCompanyAsync(client, "Societe A", "owner-rename-a@teamchat.test");
        var ownerB = await RegisterCompanyAsync(client, "Societe B", "owner-rename-b@teamchat.test");

        var response = await ApiTestHelper.SendAsync(
            client, HttpMethod.Patch, "/api/company", ownerA.Token, new { name = "A Renommee" });
        response.EnsureSuccessStatusCode();

        var loginB = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("owner-rename-b@teamchat.test", Password));
        var authB = await loginB.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal("Societe B", authB!.User!.TenantName);
        Assert.NotEqual(ownerA.TenantId, ownerB.TenantId);
    }

    [Fact]
    public async Task Admin_cannot_rename_company()
    {
        using var client = _fixture.Factory.CreateClient();
        var owner = await RegisterCompanyAsync(client, "Rename Guard", "owner-rename-guard@teamchat.test");

        var adminEmail = "admin-rename-guard@teamchat.test";
        var created = await ApiTestHelper.SendAsync(
            client, HttpMethod.Post, "/api/admin/users", owner.Token,
            new CreateUserRequest(adminEmail, "Admin Rename", Roles.Admin));
        created.EnsureSuccessStatusCode();
        var adminProfile = await created.Content.ReadFromJsonAsync<UserProfileDto>();
        await SetPasswordAsync(adminProfile!.Id, MemberPassword);
        var adminToken = await ApiTestHelper.LoginAsync(client, adminEmail, MemberPassword);

        var response = await ApiTestHelper.SendAsync(
            client, HttpMethod.Patch, "/api/company", adminToken, new { name = "Pirate" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Rename_with_empty_name_returns_400()
    {
        using var client = _fixture.Factory.CreateClient();
        var owner = await RegisterCompanyAsync(client, "Rename Empty", "owner-rename-empty@teamchat.test");

        var response = await ApiTestHelper.SendAsync(
            client, HttpMethod.Patch, "/api/company", owner.Token, new { name = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<CompanyOwner> RegisterCompanyAsync(HttpClient client, string company, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register-company",
            new RegisterCompanyRequest(company, "Owner " + company, email, Password));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return new CompanyOwner(auth!.AccessToken, auth.User!.Id, auth.User!.TenantId!.Value);
    }

    private async Task SetPasswordAsync(Guid userId, string password)
    {
        using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var user = await identity.FindByIdAsync(userId, CancellationToken.None);
        Assert.NotNull(user);
        var token = await identity.GeneratePasswordResetTokenAsync(user!, CancellationToken.None);
        var reset = await identity.ResetPasswordAsync(user!, token, password, CancellationToken.None);
        Assert.True(reset.Succeeded);
    }

    private sealed record CompanyOwner(string Token, Guid UserId, Guid TenantId);
}
