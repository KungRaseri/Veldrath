using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Veldrath.Contracts.Account;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Integration tests for <c>GET /api/account/profile</c>, the server's RBAC view of the signed-in
/// account. Everything else a player can change about their own account is Better Auth's job.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class AccountEndpointTests(WebAppFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private void SetBearer(string token) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    [Fact]
    public async Task GetProfile_Authenticated_Returns_Account_And_Roles()
    {
        var (token, accountId) = await factory.CreateAccountTokenAsync("AcctProfile_Get");
        SetBearer(token);

        var resp = await _client.GetAsync("/api/account/profile");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await resp.Content.ReadFromJsonAsync<AccountProfileDto>();
        profile!.AccountId.Should().Be(accountId);
        profile.Username.Should().Be("AcctProfile_Get");
        profile.Roles.Should().BeAssignableTo<IReadOnlyList<string>>();
        profile.Permissions.Should().NotBeNull();
    }

    [Fact]
    public async Task GetProfile_Reflects_Assigned_Role()
    {
        var (token, _) = await factory.CreateAccountTokenAsync("AcctProfile_Role", "Moderator");
        SetBearer(token);

        var resp = await _client.GetAsync("/api/account/profile");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await resp.Content.ReadFromJsonAsync<AccountProfileDto>();
        profile!.Roles.Should().Contain("Moderator");
    }

    [Fact]
    public async Task GetProfile_Reports_ThePermissions_A_Role_Carries()
    {
        // The web app gates on the profile's permission list, and a role carries its permissions as role
        // claims. Reporting only the user's own grants made a ContentEditor and a Curator look powerless,
        // which hid the whole authoring surface and the review queue from the accounts trusted with them.
        var (token, _) = await factory.CreateAccountTokenAsync("AcctProfile_RolePerms", "ContentEditor");
        SetBearer(token);

        var profile = await _client.GetFromJsonAsync<AccountProfileDto>("/api/account/profile");

        profile!.Roles.Should().Contain("ContentEditor");
        profile.Permissions.Should().Contain("content.manage");
        profile.Permissions.Should().Contain("content.view");
    }

    [Fact]
    public async Task GetProfile_Reports_The_ReviewPermission_For_A_Curator()
    {
        var (token, _) = await factory.CreateAccountTokenAsync("AcctProfile_CuratorPerms", "Curator");
        SetBearer(token);

        var profile = await _client.GetFromJsonAsync<AccountProfileDto>("/api/account/profile");

        profile!.Permissions.Should().Contain("content.review");
        profile.Permissions.Should().NotContain("content.manage", "a curator decides, it does not author");
    }

    [Fact]
    public async Task GetProfile_Unauthenticated_Returns_401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var resp = await _client.GetAsync("/api/account/profile");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SelfService_Mutations_Are_Gone()
    {
        var (token, _) = await factory.CreateAccountTokenAsync("AcctProfile_NoMutations");
        SetBearer(token);

        // Better Auth owns password/username/provider/session changes; the server must not expose them.
        (await _client.PostAsJsonAsync("/api/account/password", new { CurrentPassword = "x", NewPassword = "y" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.PutAsJsonAsync("/api/account/username", new { NewUsername = "whatever" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.GetAsync("/api/account/sessions")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.GetAsync("/api/account/providers")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
