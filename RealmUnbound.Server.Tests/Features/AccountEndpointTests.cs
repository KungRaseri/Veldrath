using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using RealmUnbound.Contracts.Account;
using RealmUnbound.Server.Data;
using RealmUnbound.Server.Data.Entities;
using RealmUnbound.Server.Tests.Infrastructure;

namespace RealmUnbound.Server.Tests.Features;

/// <summary>Integration tests for <c>GET|PUT /api/account/profile</c>,
/// <c>POST /api/account/password</c>, <c>PUT /api/account/username</c>,
/// <c>GET|DELETE /api/account/sessions[/{id}]</c>, and
/// <c>GET|DELETE /api/account/providers[/{provider}]</c>.</summary>
[Trait("Category", "Integration")]
public class AccountEndpointTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<AuthResponse> RegisterAsync(string username)
    {
        var email = $"{username.ToLowerInvariant()}@accttest.com";
        var resp = await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = email, Username = username, Password = "TestP@ssword123" });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private void SetBearer(string token) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private void ClearBearer() =>
        _client.DefaultRequestHeaders.Authorization = null;

    // ── GET /api/account/profile ──────────────────────────────────────────────

    [Fact]
    public async Task GetProfile_Authenticated_Returns_Profile()
    {
        var auth = await RegisterAsync("AcctProfile_Get");
        SetBearer(auth.AccessToken);

        var resp = await _client.GetAsync("/api/account/profile");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await resp.Content.ReadFromJsonAsync<AccountProfileDto>();
        profile!.Username.Should().Be("AcctProfile_Get");
        profile.AccountId.Should().Be(auth.AccountId);
        profile.HasPassword.Should().BeTrue();
    }

    [Fact]
    public async Task GetProfile_Unauthenticated_Returns_401()
    {
        ClearBearer();
        var resp = await _client.GetAsync("/api/account/profile");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── PUT /api/account/profile ──────────────────────────────────────────────

    [Fact]
    public async Task UpdateProfile_Sets_DisplayName_And_Bio()
    {
        var auth = await RegisterAsync("AcctProfile_Update");
        SetBearer(auth.AccessToken);

        var update = await _client.PutAsJsonAsync("/api/account/profile",
            new UpdateProfileRequest("My Display Name", "A short bio."));

        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var profile = await (await _client.GetAsync("/api/account/profile"))
            .Content.ReadFromJsonAsync<AccountProfileDto>();
        profile!.DisplayName.Should().Be("My Display Name");
        profile.Bio.Should().Be("A short bio.");
    }

    [Fact]
    public async Task UpdateProfile_ClearsFields_With_Nulls()
    {
        var auth = await RegisterAsync("AcctProfile_Clear");
        SetBearer(auth.AccessToken);

        await _client.PutAsJsonAsync("/api/account/profile",
            new UpdateProfileRequest("Name", "Bio"));
        await _client.PutAsJsonAsync("/api/account/profile",
            new UpdateProfileRequest(null, null));

        var profile = await (await _client.GetAsync("/api/account/profile"))
            .Content.ReadFromJsonAsync<AccountProfileDto>();
        profile!.DisplayName.Should().BeNull();
        profile.Bio.Should().BeNull();
    }

    // ── POST /api/account/password ────────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_WithCorrectCurrent_Returns_NoContent()
    {
        var auth = await RegisterAsync("AcctPwd_Change");
        SetBearer(auth.AccessToken);

        var resp = await _client.PostAsJsonAsync("/api/account/password",
            new ChangePasswordRequest("TestP@ssword123", "N3wP@ssword456!"));

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrent_Returns_400()
    {
        var auth = await RegisterAsync("AcctPwd_Wrong");
        SetBearer(auth.AccessToken);

        var resp = await _client.PostAsJsonAsync("/api/account/password",
            new ChangePasswordRequest("WrongCurrentPassword123!", "N3wP@ssword456!"));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_Unauthenticated_Returns_401()
    {
        ClearBearer();
        var resp = await _client.PostAsJsonAsync("/api/account/password",
            new ChangePasswordRequest("TestP@ssword123", "N3wP@ssword456!"));
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── PUT /api/account/username ─────────────────────────────────────────────

    [Fact]
    public async Task ChangeUsername_ToAvailableName_Returns_NoContent()
    {
        var auth = await RegisterAsync("AcctUname_Change");
        SetBearer(auth.AccessToken);

        var resp = await _client.PutAsJsonAsync("/api/account/username",
            new ChangeUsernameRequest("AcctUname_New"));

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ChangeUsername_ToTakenName_Returns_400()
    {
        await RegisterAsync("AcctUname_Taken");
        var auth2 = await RegisterAsync("AcctUname_Clash");
        SetBearer(auth2.AccessToken);

        var resp = await _client.PutAsJsonAsync("/api/account/username",
            new ChangeUsernameRequest("AcctUname_Taken"));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── GET /api/account/sessions ─────────────────────────────────────────────

    [Fact]
    public async Task GetSessions_Returns_At_Least_One_Session()
    {
        var auth = await RegisterAsync("AcctSessions_Get");
        SetBearer(auth.AccessToken);

        var resp = await _client.GetAsync("/api/account/sessions");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var sessions = await resp.Content.ReadFromJsonAsync<List<AccountSessionDto>>();
        sessions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetSessions_Unauthenticated_Returns_401()
    {
        ClearBearer();
        var resp = await _client.GetAsync("/api/account/sessions");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── DELETE /api/account/sessions/{id} ────────────────────────────────────

    [Fact]
    public async Task RevokeSession_OwnSession_Returns_NoContent()
    {
        var auth = await RegisterAsync("AcctSessions_Revoke");
        SetBearer(auth.AccessToken);

        var sessions = await (await _client.GetAsync("/api/account/sessions"))
            .Content.ReadFromJsonAsync<List<AccountSessionDto>>();
        var sessionId = sessions!.First().Id;

        var resp = await _client.DeleteAsync($"/api/account/sessions/{sessionId}");

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RevokeSession_AnotherUsersSession_Returns_BadRequest()
    {
        // Register two independent users and get their sessions.
        var auth1 = await RegisterAsync("AcctSessions_IDOR_A");
        var auth2 = await RegisterAsync("AcctSessions_IDOR_B");

        // Get user 1's session as user 2 — should be rejected.
        SetBearer(auth1.AccessToken);
        var sessions1 = await (await _client.GetAsync("/api/account/sessions"))
            .Content.ReadFromJsonAsync<List<AccountSessionDto>>();
        var session1Id = sessions1!.First().Id;

        SetBearer(auth2.AccessToken);
        var resp = await _client.DeleteAsync($"/api/account/sessions/{session1Id}");

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── DELETE /api/account/sessions (revoke all others) ──────────────────────

    [Fact]
    public async Task RevokeOtherSessions_Returns_NoContent()
    {
        var auth = await RegisterAsync("AcctSessions_RevokeAll");
        SetBearer(auth.AccessToken);

        // Log in again to create a second session.
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login",
            new { Email = "acctSessions_revokeall@accttest.com", Password = "TestP@ssword123" });
        var auth2 = (await loginResp.Content.ReadFromJsonAsync<AuthResponse>())!;

        var sessions = await (await _client.GetAsync("/api/account/sessions"))
            .Content.ReadFromJsonAsync<List<AccountSessionDto>>();
        sessions.Should().HaveCountGreaterThanOrEqualTo(2);

        // Revoke all except this (first) session — use a made-up current ID for simplicity.
        var resp = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/account/sessions")
        {
            Content = JsonContent.Create(new RevokeOtherSessionsRequest(Guid.NewGuid()))
        });

        // Even with an unknown "current" ID this should succeed (it just revokes them all).
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── GET /api/account/providers ────────────────────────────────────────────

    [Fact]
    public async Task GetProviders_Returns_Empty_List_For_Password_Only_Account()
    {
        var auth = await RegisterAsync("AcctProviders_Get");
        SetBearer(auth.AccessToken);

        var resp = await _client.GetAsync("/api/account/providers");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var providers = await resp.Content.ReadFromJsonAsync<List<LinkedProviderDto>>();
        // Password-only registration has no linked OAuth providers.
        providers.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProviders_Unauthenticated_Returns_401()
    {
        ClearBearer();
        var resp = await _client.GetAsync("/api/account/providers");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── DELETE /api/account/providers/{provider} ──────────────────────────────

    [Fact]
    public async Task UnlinkProvider_WhenAccountHasPassword_Succeeds()
    {
        var auth = await RegisterAsync("AcctProviders_Unlink");
        SetBearer(auth.AccessToken);

        // Manually add a fake login via UserManager so we can test unlinking.
        using var scope = factory.Services.CreateScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<PlayerAccount>>();
        var user = await userMgr.FindByNameAsync("AcctProviders_Unlink");
        await userMgr.AddLoginAsync(user!, new UserLoginInfo("Discord", "fake-discord-id-001", "Discord"));

        var resp = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/account/providers/Discord")
        {
            Content = JsonContent.Create(new UnlinkProviderRequest("fake-discord-id-001"))
        });

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UnlinkProvider_WhenOnlyLoginMethod_Returns_400()
    {
        var auth = await RegisterAsync("AcctProviders_LockGuard");
        SetBearer(auth.AccessToken);

        // Add a fake login, then remove the password so this is the only login method.
        using var scope = factory.Services.CreateScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<PlayerAccount>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await userMgr.FindByNameAsync("AcctProviders_LockGuard");
        await userMgr.AddLoginAsync(user!, new UserLoginInfo("Discord", "fake-discord-lockguard", "Discord"));
        await userMgr.RemovePasswordAsync(user!);

        var resp = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/account/providers/Discord")
        {
            Content = JsonContent.Create(new UnlinkProviderRequest("fake-discord-lockguard"))
        });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
