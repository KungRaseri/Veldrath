using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Veldrath.Server.Features.Auth;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features.Auth;

/// <summary>
/// Integration tests for <c>GET /api/auth/session</c>.
/// Verifies that valid exchange codes are redeemed, the <c>rt</c> cookie is written, and
/// the browser is redirected to the expected destination.  Disables auto-redirect so the
/// raw 302 response can be inspected.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class SessionEndpointTests(WebAppFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient(
        new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

    private readonly AuthExchangeCodeService _exchangeSvc =
        factory.Services.GetRequiredService<AuthExchangeCodeService>();

    // ── Valid code ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartSession_ValidCode_RedirectsAndSetsRtCookie()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var fakeAuth  = MakeFakeAuth(accountId, "SessionUser1");
        var code      = _exchangeSvc.CreateCode(fakeAuth, accountId);

        // Act
        var response = await _client.GetAsync(
            $"/api/auth/session?code={code}&aid={accountId}&redirectTo=http://localhost:8081/");

        // Assert — 302 redirect with the rt cookie present
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();

        var setCookieHeader = response.Headers
            .GetValues("Set-Cookie")
            .FirstOrDefault(h => h.StartsWith("rt=", StringComparison.OrdinalIgnoreCase));
        setCookieHeader.Should().NotBeNull("the rt refresh-token cookie must be set");
    }

    [Fact]
    public async Task StartSession_ValidCode_RedirectsToAllowedRedirectTo()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var code      = _exchangeSvc.CreateCode(MakeFakeAuth(accountId, "SessionUser2"), accountId);
        var redirectTo = Uri.EscapeDataString("http://localhost:8081/dashboard");

        // Act
        var response = await _client.GetAsync(
            $"/api/auth/session?code={code}&aid={accountId}&redirectTo={redirectTo}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("/dashboard");
    }

    [Fact]
    public async Task StartSession_ValidCode_CanOnlyBeRedeemedOnce()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var code      = _exchangeSvc.CreateCode(MakeFakeAuth(accountId, "SessionUser3"), accountId);
        var url       = $"/api/auth/session?code={code}&aid={accountId}&redirectTo=http://localhost:8081/";

        // Act — first call is valid, second should fail
        var first  = await _client.GetAsync(url);
        var second = await _client.GetAsync(url);

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.Redirect);
        first.Headers.GetValues("Set-Cookie")
            .Any(h => h.StartsWith("rt=", StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue();

        // Second call: no rt cookie, redirects to error
        second.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var secondCookies = second.Headers.TryGetValues("Set-Cookie", out var vals)
            ? vals.ToList() : [];
        secondCookies.Any(h => h.StartsWith("rt=", StringComparison.OrdinalIgnoreCase))
            .Should().BeFalse("a consumed code must not set the rt cookie again");
        second.Headers.Location?.ToString().Should().Contain("error=auth_failed");
    }

    // ── Missing / invalid parameters ──────────────────────────────────────────

    [Fact]
    public async Task StartSession_MissingCode_RedirectsToError()
    {
        var response = await _client.GetAsync(
            $"/api/auth/session?aid={Guid.NewGuid()}&redirectTo=http://localhost:8081/");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("error=auth_failed");
    }

    [Fact]
    public async Task StartSession_MissingAid_RedirectsToError()
    {
        var response = await _client.GetAsync(
            "/api/auth/session?code=invalid_code&redirectTo=http://localhost:8081/");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("error=auth_failed");
    }

    [Fact]
    public async Task StartSession_InvalidCode_RedirectsToError()
    {
        var response = await _client.GetAsync(
            $"/api/auth/session?code=0000000000000000000000000000000000000000000000000000000000000000&aid={Guid.NewGuid()}&redirectTo=http://localhost:8081/");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().Should().Contain("error=auth_failed");
    }

    // ── redirectTo validation ─────────────────────────────────────────────────

    [Fact]
    public async Task StartSession_InvalidCode_UntrustedRedirectTo_StillRedirectsToError()
    {
        // Even when redirectTo is untrusted, the error path should not open-redirect.
        // ErrorRedirect falls back to /login?error=auth_failed on an unknown host.
        var response = await _client.GetAsync(
            $"/api/auth/session?code=bad&aid={Guid.NewGuid()}&redirectTo=https://evil.example.com/");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        location.Should().NotContain("evil.example.com",
            "error redirect must never follow an untrusted host");
        location.Should().Contain("error=auth_failed");
    }

    [Fact]
    public async Task StartSession_ValidCode_UntrustedRedirectTo_FallsBackToFoundry()
    {
        // Even with a valid code, an untrusted redirectTo must be ignored.
        var accountId = Guid.NewGuid();
        var code      = _exchangeSvc.CreateCode(MakeFakeAuth(accountId, "SessionUser4"), accountId);

        var response = await _client.GetAsync(
            $"/api/auth/session?code={code}&aid={accountId}&redirectTo=https://evil.example.com/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        location.Should().NotContain("evil.example.com",
            "a valid code must not be used to open-redirect to an untrusted host");

        // rt cookie should still be set — the session is valid, only the destination changes
        var rtCookie = response.Headers
            .GetValues("Set-Cookie")
            .Any(h => h.StartsWith("rt=", StringComparison.OrdinalIgnoreCase));
        rtCookie.Should().BeTrue("the rt cookie should still be issued even when redirectTo is untrusted");
    }

    // ── IsAllowedReturnUrl unit tests ─────────────────────────────────────────

    [Theory]
    [InlineData("http://localhost:8081/", null, null, true)]
    [InlineData("http://127.0.0.1:8081/", null, null, true)]
    [InlineData("https://foundry.veldrath.com/", "https://foundry.veldrath.com", null, true)]
    [InlineData("https://veldrath.com/", null, "https://veldrath.com", true)]
    [InlineData("https://evil.example.com/", "https://foundry.veldrath.com", "https://veldrath.com", false)]
    [InlineData("ftp://localhost/", null, null, false)]
    [InlineData("not-a-url", null, null, false)]
    [InlineData("https://foundry.veldrath.com.evil.com/", "https://foundry.veldrath.com", null, false)]
    public void IsAllowedReturnUrl_ReturnsExpectedResult(
        string url, string? foundryBase, string? additionalBase, bool expected)
    {
        ExternalAuthEndpoints.IsAllowedReturnUrl(url, foundryBase, additionalBase)
            .Should().Be(expected);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static AuthResponse MakeFakeAuth(Guid accountId, string username) =>
        new(
            AccessToken:       "fake-access-token",
            RefreshToken:      $"fake-refresh-{Guid.NewGuid():N}",
            AccessTokenExpiry: DateTimeOffset.UtcNow.AddMinutes(15),
            AccountId:         accountId,
            Username:          username,
            Roles:             [],
            Characters:        []);
}
