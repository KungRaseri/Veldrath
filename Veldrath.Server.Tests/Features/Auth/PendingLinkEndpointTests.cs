using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Features.Auth;
using Veldrath.Server.Infrastructure.Email;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features.Auth;

/// <summary>
/// Integration tests for <c>GET /api/auth/link/confirm</c>.
/// Uses <see cref="WebAppFactory"/> with an in-memory SQLite database.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class PendingLinkEndpointTests(WebAppFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient(
        new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

    // ── Valid token → exchange code issued ────────────────────────────────────

    [Fact]
    public async Task ConfirmLink_ValidToken_Redirects_With_ExchangeCode()
    {
        // Arrange — create an account and a valid pending-link token
        var (account, rawToken) = await SeedPendingLinkAsync(
            email: "confirm_valid@test.com",
            username: "ConfirmValid",
            expiresAt: DateTimeOffset.UtcNow.AddHours(1));

        // Act
        var response = await _client.GetAsync($"/api/auth/link/confirm?token={rawToken}");

        // Assert — redirect with an exchange code in the Location header
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        location.Should().Contain("code=");
        location.Should().Contain($"aid={account.Id}");
    }

    [Fact]
    public async Task ConfirmLink_ValidToken_ExchangeCode_Is_Redeemable()
    {
        // Arrange
        var (_, rawToken) = await SeedPendingLinkAsync(
            email: "confirm_redeem@test.com",
            username: "ConfirmRedeem",
            expiresAt: DateTimeOffset.UtcNow.AddHours(1));

        // Act — confirm the link
        var confirmResponse = await _client.GetAsync($"/api/auth/link/confirm?token={rawToken}");
        var location        = confirmResponse.Headers.Location?.ToString() ?? string.Empty;

        // Extract the code from the redirect URL
        var uri   = new Uri("http://localhost" + location); // Location is relative
        var code  = System.Web.HttpUtility.ParseQueryString(uri.Query)["code"];
        var aidStr = System.Web.HttpUtility.ParseQueryString(uri.Query)["aid"];
        code.Should().NotBeNullOrEmpty();
        aidStr.Should().NotBeNullOrEmpty();

        // Redeem the exchange code
        var exchangeResponse = await _client.PostAsJsonAsync("/api/auth/exchange",
            new Veldrath.Contracts.Auth.ExchangeCodeRequest(code!, Guid.Parse(aidStr!)));

        exchangeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await exchangeResponse.Content.ReadFromJsonAsync<AuthResponse>();
        auth!.AccessToken.Should().NotBeEmpty();
    }

    // ── Expired token ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmLink_ExpiredToken_Redirects_To_ErrorPage()
    {
        // Arrange
        var (_, rawToken) = await SeedPendingLinkAsync(
            email: "confirm_expired@test.com",
            username: "ConfirmExpired",
            expiresAt: DateTimeOffset.UtcNow.AddHours(-1)); // already expired

        // Act
        var response = await _client.GetAsync($"/api/auth/link/confirm?token={rawToken}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        location.Should().Contain("error=link_expired");
    }

    // ── Unknown token ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmLink_UnknownToken_Redirects_To_ErrorPage()
    {
        // Generate a token that was never persisted
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

        var response = await _client.GetAsync($"/api/auth/link/confirm?token={rawToken}");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        location.Should().Contain("error=link_invalid");
    }

    // ── Cannot reuse a confirmed token ────────────────────────────────────────

    [Fact]
    public async Task ConfirmLink_AlreadyConfirmedToken_Redirects_To_ErrorPage()
    {
        // Arrange — confirm once first
        var (_, rawToken) = await SeedPendingLinkAsync(
            email: "confirm_reuse@test.com",
            username: "ConfirmReuse",
            expiresAt: DateTimeOffset.UtcNow.AddHours(1));

        await _client.GetAsync($"/api/auth/link/confirm?token={rawToken}");

        // Act — attempt to confirm the same token again
        var secondResponse = await _client.GetAsync($"/api/auth/link/confirm?token={rawToken}");

        // Assert
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = secondResponse.Headers.Location?.ToString() ?? string.Empty;
        location.Should().Contain("error=link_already_confirmed");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a <see cref="PlayerAccount"/> and a corresponding <see cref="PendingLinkToken"/> directly
    /// via EF Core, bypassing the email-send flow so no SMTP is needed.
    /// Returns the account and the unhashed raw token to pass to the endpoint.
    /// </summary>
    private async Task<(PlayerAccount Account, string RawToken)> SeedPendingLinkAsync(
        string email, string username, DateTimeOffset expiresAt)
    {
        // Register an account via the API so Identity hashes the password properly.
        await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = email, Username = username, Password = "TestP@ssword123" });

        using var scope   = factory.Services.CreateScope();
        var db            = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userMgr       = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<PlayerAccount>>();

        var account  = await userMgr.FindByEmailAsync(email)
                       ?? throw new InvalidOperationException("Account not found after registration.");

        var rawToken  = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var tokenHash = AuthService.HashToken(rawToken);

        db.PendingLinkTokens.Add(new PendingLinkToken
        {
            AccountId     = account.Id,
            LoginProvider = "Discord",
            ProviderKey   = $"disc-{Guid.NewGuid():N}",
            TokenHash     = tokenHash,
            Email         = email,
            ExpiresAt     = expiresAt,
            ReturnUrl     = null, // no returnUrl → redirect to /login?code=...
        });
        await db.SaveChangesAsync();

        return (account, rawToken);
    }
}
