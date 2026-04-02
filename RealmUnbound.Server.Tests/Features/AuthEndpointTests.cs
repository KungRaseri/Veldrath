using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealmUnbound.Server.Data;
using RealmUnbound.Server.Tests.Infrastructure;

namespace RealmUnbound.Server.Tests.Features;

[Trait("Category", "Integration")]
public class AuthEndpointTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Register_Should_Return_Token_Pair()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = "auth_register_user@test.com", Username = "Auth_Register_User", Password = "Pass1234!" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.AccessToken.Should().NotBeEmpty();
        body.RefreshToken.Should().NotBeEmpty();
        body.AccountId.Should().NotBe(Guid.Empty);
        body.Username.Should().Be("Auth_Register_User");
    }

    [Fact]
    public async Task Register_Should_Reject_Duplicate_Username()
    {
        await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = "auth_dupe_user@test.com", Username = "Auth_Dupe_User", Password = "Pass1234!" });

        var second = await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = "auth_dupe_user2@test.com", Username = "Auth_Dupe_User", Password = "Pass1234!" });

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_Should_Reject_Weak_Password()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = "auth_weak_pass_user@test.com", Username = "Auth_Weak_Pass_User", Password = "abc" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_Should_Return_Token_Pair()
    {
        await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = "auth_login_user@test.com", Username = "Auth_Login_User", Password = "Pass1234!" });

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { Email = "auth_login_user@test.com", Password = "Pass1234!" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.AccessToken.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Login_Should_Return_Unauthorized_For_Wrong_Password()
    {
        await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = "auth_badpass_user@test.com", Username = "Auth_BadPass_User", Password = "Pass1234!" });

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { Email = "auth_badpass_user@test.com", Password = "WrongPassword!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_Should_Return_Unauthorized_For_Unknown_User()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { Email = "nosuchuser@test.com", Password = "Pass1234!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_Should_Issue_New_Token_Pair()
    {
        var reg = await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = "auth_refresh_user@test.com", Username = "Auth_Refresh_User", Password = "Pass1234!" });
        var auth = await reg.Content.ReadFromJsonAsync<AuthResponse>();

        var response = await _client.PostAsJsonAsync("/api/auth/refresh",
            new { RefreshToken = auth!.RefreshToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var newAuth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        newAuth!.RefreshToken.Should().NotBe(auth.RefreshToken);
        newAuth.AccessToken.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Refresh_Should_Return_Unauthorized_For_Invalid_Token()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/refresh",
            new { RefreshToken = "totally-invalid-token" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_Should_Revoke_All_Tokens_When_Revoked_Token_Is_Reused()
    {
        var reg = await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = "auth_theft_user@test.com", Username = "Auth_Theft_User", Password = "Pass1234!" });
        var auth = await reg.Content.ReadFromJsonAsync<AuthResponse>();

        // Use the refresh token once (rotates it out).
        await _client.PostAsJsonAsync("/api/auth/refresh",
            new { RefreshToken = auth!.RefreshToken });

        // Present the now-revoked original token — theft detection; all tokens revoked.
        var stolen = await _client.PostAsJsonAsync("/api/auth/refresh",
            new { RefreshToken = auth.RefreshToken });

        stolen.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_Should_Revoke_Refresh_Token()
    {
        var reg = await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = "auth_logout_user@test.com", Username = "Auth_Logout_User", Password = "Pass1234!" });
        var auth = await reg.Content.ReadFromJsonAsync<AuthResponse>();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var logout = await _client.PostAsJsonAsync("/api/auth/logout",
            new { RefreshToken = auth.RefreshToken });

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Refresh should now fail.
        var refresh = await _client.PostAsJsonAsync("/api/auth/refresh",
            new { RefreshToken = auth.RefreshToken });

        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_Should_Require_Authentication()
    {
        using var unauthClient = factory.CreateClient();
        var response = await unauthClient.PostAsJsonAsync("/api/auth/logout",
            new { RefreshToken = "any-token" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_Should_Reject_Duplicate_Email()
    {
        await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = "dupe_email@test.com", Username = "Auth_DupeEmail_User1", Password = "Pass1234!" });

        var second = await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = "dupe_email@test.com", Username = "Auth_DupeEmail_User2", Password = "Pass1234!" });

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_Should_Return_Correct_Username_In_Response()
    {
        const string username = "Auth_Username_Return_User";
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = "auth_username_return@test.com", Username = username, Password = "Pass1234!" });

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.Username.Should().Be(username);
        body.AccountId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Refresh_Should_Preserve_Same_AccountId()
    {
        var reg = await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = "auth_preserve_account@test.com", Username = "Auth_PreserveAcct_User", Password = "Pass1234!" });
        var original = await reg.Content.ReadFromJsonAsync<AuthResponse>();

        var refreshed = await (await _client.PostAsJsonAsync("/api/auth/refresh",
            new { RefreshToken = original!.RefreshToken }))
            .Content.ReadFromJsonAsync<AuthResponse>();

        refreshed!.AccountId.Should().Be(original.AccountId);
        refreshed.Username.Should().Be(original.Username);
    }

    [Fact]
    public async Task Login_Should_Return_Same_AccountId_As_Registration()
    {
        var reg = await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = "auth_same_accountid@test.com", Username = "Auth_SameAcctId_User", Password = "Pass1234!" });
        var registered = await reg.Content.ReadFromJsonAsync<AuthResponse>();

        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new { Email = "auth_same_accountid@test.com", Password = "Pass1234!" });
        var loggedIn = await login.Content.ReadFromJsonAsync<AuthResponse>();

        loggedIn!.AccountId.Should().Be(registered!.AccountId);
    }

    [Fact]
    public async Task Refresh_Should_Rotate_Token_So_Old_Token_Is_Invalid()
    {
        var reg = await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = "auth_rotate_check@test.com", Username = "Auth_RotateCheck_User", Password = "Pass1234!" });
        var original = await reg.Content.ReadFromJsonAsync<AuthResponse>();

        // Use the refresh token once → gets a new token
        await _client.PostAsJsonAsync("/api/auth/refresh",
            new { RefreshToken = original!.RefreshToken });

        // Old refresh token should now be invalid
        var oldAttempt = await _client.PostAsJsonAsync("/api/auth/refresh",
            new { RefreshToken = original.RefreshToken });

        oldAttempt.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_Should_Include_Non_Expired_Access_Token()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = "auth_expiry@test.com", Username = "Auth_Expiry_User", Password = "Pass1234!" });

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.AccessTokenExpiry.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Refresh_Should_Return_Unauthorized_For_Expired_Token()
    {
        // Register so we have a real, valid-format token in the DB.
        var reg = await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = "auth_expired_refresh@test.com", Username = "Auth_ExpiredRefresh_User", Password = "Pass1234!" });
        var auth = await reg.Content.ReadFromJsonAsync<AuthResponse>();

        // Manually expire the token in the DB (without revoking it) to simulate natural expiry.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(auth!.RefreshToken))).ToLowerInvariant();
            var stored = await db.RefreshTokens.FirstAsync(t => t.TokenHash == hash);
            stored.ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/api/auth/refresh",
            new { auth!.RefreshToken });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_Should_Return_Lockout_Message_After_Max_Failed_Attempts()
    {
        const string email    = "auth_lockout_target@test.com";
        const string username = "Auth_Lockout_Target";
        await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = email, Username = username, Password = "Pass1234!" });

        // Exhaust attempts (MaxFailedAccessAttempts = 5)
        for (var i = 0; i < 5; i++)
            await _client.PostAsJsonAsync("/api/auth/login",
                new { Email = email, Password = "WrongPass!" });

        // Locked-out attempt
        var locked = await _client.PostAsJsonAsync("/api/auth/login",
            new { Email = email, Password = "WrongPass!" });

        locked.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await locked.Content.ReadAsStringAsync();
        body.Should().ContainEquivalentOf("locked out");
    }
}
