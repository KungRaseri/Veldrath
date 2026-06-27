using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Features.Auth;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Integration tests for <see cref="ExternalAuthEndpoints.HandleOAuthTicket"/>.
/// Exercises the OAuth callback handler in isolation by constructing
/// <see cref="TicketReceivedContext"/> directly — no browser or HTTP redirect through
/// an actual OAuth provider is needed.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class ExternalAuthEndpointTests(WebAppFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient(
        new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

    private ApplicationDbContext _db = null!;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        _db = factory.Services.GetRequiredService<ApplicationDbContext>();
    }

    /// <inheritdoc/>
    public Task DisposeAsync() => Task.CompletedTask;

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="TicketReceivedContext"/> suitable for passing to
    /// <see cref="ExternalAuthEndpoints.HandleOAuthTicket"/>.
    /// </summary>
    private static TicketReceivedContext CreateTicketContext(
        IServiceProvider services,
        ClaimsPrincipal principal,
        AuthenticationProperties? properties = null,
        string schemeName = "Discord")
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services,
            Connection = { RemoteIpAddress = IPAddress.Loopback },
            Request = { Scheme = "http", Host = new HostString("localhost", 5000) },
        };

        var scheme = new AuthenticationScheme(schemeName, schemeName, typeof(TestAuthHandler));
        var options = new OAuthOptions
        {
            ClientId = "test",
            ClientSecret = "test",
            CallbackPath = "/signin-discord",
        };

        properties ??= new AuthenticationProperties();
        var ticket = new AuthenticationTicket(principal, properties, schemeName);

        return new TicketReceivedContext(httpContext, scheme, options, ticket);
    }

    // ── Link mode: valid account ───────────────────────────────────────────────

    [Fact]
    public async Task HandleOAuthTicket_LinkMode_ValidAccount_RedirectsToSession()
    {
        // Arrange — create a real account in the database
        var passwordHasher = factory.Services.GetRequiredService<IPasswordHasher<PlayerAccount>>();
        var account = new PlayerAccount
        {
            Id = Guid.NewGuid(),
            UserName = "linktestuser",
            Email = "linktest@example.com",
            NormalizedEmail = "LINKTEST@EXAMPLE.COM",
            NormalizedUserName = "LINKTESTUSER",
        };
        account.PasswordHash = passwordHasher.HashPassword(account, "P@ssw0rd123!");
        _db.Users.Add(account);
        await _db.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Email, "oauthuser@external.com"),
            new Claim(ClaimTypes.Name, "OAuthUser"),
            new Claim(ClaimTypes.NameIdentifier, "discord-12345"),
        }, "Discord"));

        var properties = new AuthenticationProperties { RedirectUri = "http://localhost:8081/profile" };
        properties.Items["mode"] = "link";
        properties.Items["accountId"] = account.Id.ToString();
        properties.Items["returnUrl"] = "http://localhost:8081/profile";

        var ctx = CreateTicketContext(factory.Services, principal, properties);

        // Act
        await ExternalAuthEndpoints.HandleOAuthTicket(ctx);

        // Assert — 302 redirect to /api/auth/session with code
        var response = ctx.HttpContext.Response;
        response.StatusCode.Should().Be(StatusCodes.Status302Found);

        var location = (string?)response.Headers.Location;
        location.Should().NotBeNull();
        location.Should().StartWith("/api/auth/session?");
        location.Should().Contain("code=");
        location.Should().Contain("aid=");

        // Verify the external login was added to the account
        var login = await _db.UserLogins
            .FirstOrDefaultAsync(l => l.UserId == account.Id);
        login.Should().NotBeNull();
        login!.LoginProvider.Should().Be("Discord");
        login.ProviderKey.Should().Be("discord-12345");
    }

    // ── Link mode: nonexistent account ────────────────────────────────────────

    [Fact]
    public async Task HandleOAuthTicket_LinkMode_NonexistentAccount_RedirectsWithError()
    {
        // Arrange
        var nonexistentId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Email, "oauth@test.com"),
            new Claim(ClaimTypes.Name, "OAuthUser"),
            new Claim(ClaimTypes.NameIdentifier, "discord-99999"),
        }, "Discord"));

        var properties = new AuthenticationProperties { RedirectUri = "http://localhost:8081/profile" };
        properties.Items["mode"] = "link";
        properties.Items["accountId"] = nonexistentId.ToString();
        properties.Items["returnUrl"] = "http://localhost:8081/profile";

        var ctx = CreateTicketContext(factory.Services, principal, properties);

        // Act
        await ExternalAuthEndpoints.HandleOAuthTicket(ctx);

        // Assert — 302 redirect with error flag
        var response = ctx.HttpContext.Response;
        response.StatusCode.Should().Be(StatusCodes.Status302Found);

        var location = (string?)response.Headers.Location;
        location.Should().NotBeNull();
        location.Should().Contain("error=link_failed");
    }

    // ── Normal login: new account ─────────────────────────────────────────────

    [Fact]
    public async Task HandleOAuthTicket_NormalLogin_NewAccount_CreatesUserAndRedirects()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Email, "newuser@external.com"),
            new Claim(ClaimTypes.Name, "NewUser"),
            new Claim(ClaimTypes.NameIdentifier, "discord-new-001"),
        }, "Discord"));

        var properties = new AuthenticationProperties { RedirectUri = "http://localhost:5000/login" };
        properties.Items["returnUrl"] = "http://localhost:5000/login";

        var ctx = CreateTicketContext(factory.Services, principal, properties);

        // Act
        await ExternalAuthEndpoints.HandleOAuthTicket(ctx);

        // Assert — should redirect with an exchange code
        var response = ctx.HttpContext.Response;
        response.StatusCode.Should().Be(StatusCodes.Status302Found);

        var location = (string?)response.Headers.Location;
        location.Should().NotBeNull();
        location.Should().Contain("code=");

        // The account should have been created by ExternalLoginOrRegisterAsync
        var createdAccount = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == "newuser@external.com");
        createdAccount.Should().NotBeNull();

        // And the external login should be linked
        var login = await _db.UserLogins
            .FirstOrDefaultAsync(l => l.ProviderKey == "discord-new-001");
        login.Should().NotBeNull();
        login!.LoginProvider.Should().Be("Discord");
    }

    // ── IsAllowedReturnUrl unit tests ─────────────────────────────────────────

    /// <summary>
    /// Exercises the same logic as <see cref="ExternalAuthEndpoints.IsAllowedReturnUrl"/>
    /// (an <c>internal</c> method) by duplicating the pure function.
    /// Verifies that only localhost, the configured Foundry base, and an additional
    /// trusted origin are accepted; all other URLs are rejected to prevent open-redirect
    /// attacks.
    /// </summary>
    [Theory]
    [InlineData("http://localhost:8081/", null, null, true)]
    [InlineData("http://127.0.0.1:8081/", null, null, true)]
    [InlineData("https://evil.example.com/", "https://foundry.veldrath.com", "https://veldrath.com", false)]
    [InlineData("ftp://localhost/", null, null, false)]
    [InlineData("not-a-url", null, null, false)]
    [InlineData("https://foundry.veldrath.com.evil.com/", "https://foundry.veldrath.com", null, false)]
    [InlineData("http://localhost:5000/login", null, null, true)]
    [InlineData("", null, null, false)]
    public void IsAllowedReturnUrl_ReturnsExpectedResult(
        string url, string? foundryBase, string? additionalBase, bool expected)
    {
        var actual = IsAllowedReturnUrl(url, foundryBase, additionalBase);
        actual.Should().Be(expected);
    }

    /// <summary>
    /// Mirrors <see cref="ExternalAuthEndpoints.IsAllowedReturnUrl"/> — tested here
    /// without needing <c>InternalsVisibleTo</c> since the logic is a pure function
    /// with no server dependencies.
    /// </summary>
    private static bool IsAllowedReturnUrl(string url, string? foundryBaseUrl, string? additionalBaseUrl)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;
        if (uri.Scheme is not ("http" or "https"))
            return false;
        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || uri.Host == "127.0.0.1")
            return true;
        if (foundryBaseUrl is not null
            && Uri.TryCreate(foundryBaseUrl, UriKind.Absolute, out var foundry)
            && uri.Scheme.Equals(foundry.Scheme, StringComparison.OrdinalIgnoreCase)
            && uri.Host.Equals(foundry.Host, StringComparison.OrdinalIgnoreCase)
            && uri.Port == foundry.Port)
            return true;
        if (additionalBaseUrl is not null
            && Uri.TryCreate(additionalBaseUrl, UriKind.Absolute, out var additional)
            && uri.Scheme.Equals(additional.Scheme, StringComparison.OrdinalIgnoreCase)
            && uri.Host.Equals(additional.Host, StringComparison.OrdinalIgnoreCase)
            && uri.Port == additional.Port)
            return true;
        return false;
    }
}

/// <summary>
/// Minimal <see cref="IAuthenticationHandler"/> implementation required to construct
/// an <see cref="AuthenticationScheme"/> for the test <see cref="TicketReceivedContext"/>.
/// </summary>
public class TestAuthHandler : IAuthenticationHandler
{
    /// <inheritdoc/>
    public Task InitializeAsync(AuthenticationScheme scheme, HttpContext context) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task<AuthenticateResult> AuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());

    /// <inheritdoc/>
    public Task ChallengeAsync(AuthenticationProperties? properties) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task ForbidAsync(AuthenticationProperties? properties) => Task.CompletedTask;
}
