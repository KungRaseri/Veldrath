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
