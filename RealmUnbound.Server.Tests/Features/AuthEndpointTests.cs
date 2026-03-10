using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
            new { Username = "Auth_Register_User", Password = "Pass1234!" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResult>();
        body!.AccessToken.Should().NotBeEmpty();
        body.RefreshToken.Should().NotBeEmpty();
        body.AccountId.Should().NotBe(Guid.Empty);
        body.Username.Should().Be("Auth_Register_User");
    }

    [Fact]
    public async Task Register_Should_Reject_Duplicate_Username()
    {
        await _client.PostAsJsonAsync("/api/auth/register",
            new { Username = "Auth_Dupe_User", Password = "Pass1234!" });

        var second = await _client.PostAsJsonAsync("/api/auth/register",
            new { Username = "Auth_Dupe_User", Password = "Pass1234!" });

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_Should_Reject_Weak_Password()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { Username = "Auth_Weak_Pass_User", Password = "abc" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_Should_Return_Token_Pair()
    {
        await _client.PostAsJsonAsync("/api/auth/register",
            new { Username = "Auth_Login_User", Password = "Pass1234!" });

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { Username = "Auth_Login_User", Password = "Pass1234!" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResult>();
        body!.AccessToken.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Login_Should_Return_Unauthorized_For_Wrong_Password()
    {
        await _client.PostAsJsonAsync("/api/auth/register",
            new { Username = "Auth_BadPass_User", Password = "Pass1234!" });

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { Username = "Auth_BadPass_User", Password = "WrongPassword!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_Should_Return_Unauthorized_For_Unknown_User()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { Username = "NoSuchUser_XYZ", Password = "Pass1234!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_Should_Issue_New_Token_Pair()
    {
        var reg = await _client.PostAsJsonAsync("/api/auth/register",
            new { Username = "Auth_Refresh_User", Password = "Pass1234!" });
        var auth = await reg.Content.ReadFromJsonAsync<AuthResult>();

        var response = await _client.PostAsJsonAsync("/api/auth/refresh",
            new { RefreshToken = auth!.RefreshToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var newAuth = await response.Content.ReadFromJsonAsync<AuthResult>();
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
            new { Username = "Auth_Theft_User", Password = "Pass1234!" });
        var auth = await reg.Content.ReadFromJsonAsync<AuthResult>();

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
            new { Username = "Auth_Logout_User", Password = "Pass1234!" });
        var auth = await reg.Content.ReadFromJsonAsync<AuthResult>();

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
}
