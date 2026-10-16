using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Auth.Dtos;
using TeamChat.Domain.Constants;
using TeamChat.IntegrationTests.Support;
using CreateUserRequest = TeamChat.Application.Users.Dtos.CreateUserRequest;
using UpdateUserRequest = TeamChat.Application.Users.Dtos.UpdateUserRequest;

namespace TeamChat.IntegrationTests.Api;

/// <summary>
/// Isolations et regles multi-societes : inscription d'une societe, cloture
/// des donnees par societe et permissions Owner / Admin.
/// </summary>
[Collection("api")]
public sealed class MultiTenancyTests
{
    private const string OwnerPassword = "OwnerPass123!";
    private const string MemberPassword = "MemberPass123!";

    private readonly ApiCollectionFixture _fixture;

    public MultiTenancyTests(ApiCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Register_company_creates_owner_and_accessible_login()
    {
        var email = "owner-acme@teamchat.test";
        using var client = _fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register-company",
            new RegisterCompanyRequest("Acme Corp", "Alice Owner", email, OwnerPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.NotNull(auth!.User);
        Assert.Equal(Roles.Owner, auth.User!.Role);
        Assert.Equal("Acme Corp", auth.User.TenantName);
        Assert.NotNull(auth.User.TenantId);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, OwnerPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var logged = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal(auth.User.TenantId, logged!.User!.TenantId);
        Assert.Equal("Acme Corp", logged.User.TenantName);
    }

    [Fact]
    public async Task Register_company_with_existing_email_returns_409()
    {
        var email = "owner-duplicate@teamchat.test";
        using var client = _fixture.Factory.CreateClient();

        var first = await client.PostAsJsonAsync("/api/auth/register-company",
            new RegisterCompanyRequest("Societe Un", "Owner Un", email, OwnerPassword));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/auth/register-company",
            new RegisterCompanyRequest("Societe Deux", "Owner Deux", email, OwnerPassword));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_company_with_weak_password_returns_400()
    {
        using var client = _fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register-company",
            new RegisterCompanyRequest("Faible", "Owner Faible", "owner-faible@teamchat.test", "faible"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Users_directory_is_scoped_to_the_company()
    {
        using var client = _fixture.Factory.CreateClient();
        var ownerA = await RegisterCompanyAsync(client, "Alpha", "owner-alpha@teamchat.test");
        var ownerB = await RegisterCompanyAsync(client, "Beta", "owner-beta@teamchat.test");

        // Requete authentifiee : l'annuaire de Alpha ne doit pas voir owner-beta.
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/users?search=owner-beta");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerA.Token);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<UserListItemDto>>();

        Assert.NotNull(result);
        Assert.DoesNotContain(result!.Items, u => u.DisplayName.Contains("Beta", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Admin_list_only_returns_users_of_own_company()
    {
        using var client = _fixture.Factory.CreateClient();
        var ownerA = await RegisterCompanyAsync(client, "Gamma", "owner-gamma@teamchat.test");
        var ownerB = await RegisterCompanyAsync(client, "Delta", "owner-delta@teamchat.test");

        var listA = await GetAdminUsersAsync(client, ownerA.Token);
        var listB = await GetAdminUsersAsync(client, ownerB.Token);

        Assert.Single(listA.Items);
        Assert.Equal(ownerA.UserId, listA.Items[0].Id);
        Assert.Single(listB.Items);
        Assert.Equal(ownerB.UserId, listB.Items[0].Id);
    }

    [Fact]
    public async Task Owner_can_create_admin_but_admin_cannot_create_admin()
    {
        using var client = _fixture.Factory.CreateClient();
        var owner = await RegisterCompanyAsync(client, "Omega", "owner-omega@teamchat.test");

        var adminEmail = "admin-omega@teamchat.test";
        var created = await CreateUserAsync(client, owner.Token, adminEmail, "Admin Omega", Roles.Admin);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var adminProfile = await created.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.Equal(Roles.Admin, adminProfile!.Role);

        await SetPasswordAsync(adminProfile.Id, MemberPassword);
        var adminToken = await ApiTestHelper.LoginAsync(client, adminEmail, MemberPassword);

        var forbidden = await CreateUserAsync(client, adminToken, "admin-bis@teamchat.test", "Admin Bis", Roles.Admin);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var allowed = await CreateUserAsync(client, adminToken, "membre-omega@teamchat.test", "Membre Omega", Roles.User);
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
    }

    [Fact]
    public async Task Admin_cannot_modify_or_delete_owner()
    {
        using var client = _fixture.Factory.CreateClient();
        var owner = await RegisterCompanyAsync(client, "Sigma", "owner-sigma@teamchat.test");
        var adminEmail = "admin-sigma@teamchat.test";
        var created = await CreateUserAsync(client, owner.Token, adminEmail, "Admin Sigma", Roles.Admin);
        var adminProfile = await created.Content.ReadFromJsonAsync<UserProfileDto>();
        await SetPasswordAsync(adminProfile!.Id, MemberPassword);
        var adminToken = await ApiTestHelper.LoginAsync(client, adminEmail, MemberPassword);

        var update = await ApiTestHelper.SendAsync(
            client, HttpMethod.Patch, $"/api/admin/users/{owner.UserId}", adminToken,
            new UpdateUserRequest(null, Roles.User, null));
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);

        var delete = await ApiTestHelper.SendAsync(
            client, HttpMethod.Delete, $"/api/admin/users/{owner.UserId}", adminToken);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Super_admin_is_not_attached_to_any_company()
    {
        using var client = _fixture.Factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(ApiCollectionFixture.SuperAdminEmail, ApiCollectionFixture.SuperAdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(auth);
        Assert.Equal(Roles.SuperAdmin, auth!.User!.Role);
        Assert.Null(auth.User.TenantId);
    }

    private async Task<CompanyOwner> RegisterCompanyAsync(HttpClient client, string company, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register-company",
            new RegisterCompanyRequest(company, "Owner " + company, email, OwnerPassword));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return new CompanyOwner(auth!.AccessToken, auth.User!.Id);
    }

    private static async Task<PagedResult<UserProfileDto>> GetAdminUsersAsync(HttpClient client, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/users");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PagedResult<UserProfileDto>>())!;
    }

    private static Task<HttpResponseMessage> CreateUserAsync(
        HttpClient client, string token, string email, string displayName, string role)
        => ApiTestHelper.SendAsync(
            client, HttpMethod.Post, "/api/admin/users", token,
            new CreateUserRequest(email, displayName, role, MemberPassword));

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

    private sealed record CompanyOwner(string Token, Guid UserId);
}
