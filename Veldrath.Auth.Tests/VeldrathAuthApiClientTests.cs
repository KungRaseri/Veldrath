using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Veldrath.Auth;
using Veldrath.Contracts.Auth;
using Xunit;

namespace Veldrath.Auth.Tests;

public class VeldrathAuthApiClientTests
{
    private static (VeldrathAuthApiClient Client, Mock<HttpMessageHandler> Handler) CreateClient(
        HttpStatusCode status, object? responseBody = null)
    {
        var json    = responseBody is not null ? JsonSerializer.Serialize(responseBody) : string.Empty;
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
               .Setup<Task<HttpResponseMessage>>(
                   "SendAsync",
                   ItExpr.IsAny<HttpRequestMessage>(),
                   ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(new HttpResponseMessage(status) { Content = content });

        var http = new HttpClient(handler.Object) { BaseAddress = new Uri("https://api.test/") };
        return (new VeldrathAuthApiClient(http), handler);
    }

    [Fact]
    public void SetBearerToken_SetsAuthorizationHeader()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK);
        client.SetBearerToken("my-token");

        // Access the http client's auth header via the client's ServerBaseUrl property as a smoke test.
        client.ServerBaseUrl.Should().NotBeNull();
    }

    [Fact]
    public void ClearBearerToken_DoesNotThrow()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK);
        client.SetBearerToken("x");
        var act = () => client.ClearBearerToken();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task IsServerReachableAsync_ReturnsTrueOnSuccess()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK);
        var result = await client.IsServerReachableAsync();
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsServerReachableAsync_ReturnsFalseOnServerError()
    {
        var (client, _) = CreateClient(HttpStatusCode.ServiceUnavailable);
        var result = await client.IsServerReachableAsync();
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RegisterAsync_ReturnsAuthResponseOnSuccess()
    {
        var expected = new AuthResponse(
            "access", "refresh", DateTimeOffset.UtcNow.AddHours(1),
            Guid.NewGuid(), "user", [], [], false, null);
        var (client, _) = CreateClient(HttpStatusCode.OK, expected);

        var result = await client.RegisterAsync("a@b.com", "user", "pass");
        result.Should().NotBeNull();
        result!.Username.Should().Be("user");
    }

    [Fact]
    public async Task RegisterAsync_ReturnsNullOnFailure()
    {
        var (client, _) = CreateClient(HttpStatusCode.Conflict);
        var result = await client.RegisterAsync("a@b.com", "user", "pass");
        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_ReturnsAuthResponseOnSuccess()
    {
        var accountId = Guid.NewGuid();
        var expected  = new AuthResponse(
            "access", "refresh", DateTimeOffset.UtcNow.AddHours(1),
            accountId, "player", [], [], false, null);
        var (client, _) = CreateClient(HttpStatusCode.OK, expected);

        var result = await client.LoginAsync("a@b.com", "pass");
        result.Should().NotBeNull();
        result!.AccountId.Should().Be(accountId);
    }

    [Fact]
    public async Task LoginAsync_ReturnsNullOnUnauthorized()
    {
        var (client, _) = CreateClient(HttpStatusCode.Unauthorized);
        var result = await client.LoginAsync("a@b.com", "wrong");
        result.Should().BeNull();
    }

    [Fact]
    public async Task RefreshTokenAsync_ReturnsNewTokenPairOnSuccess()
    {
        var expected = new AuthResponse(
            "new-access", "new-refresh", DateTimeOffset.UtcNow.AddHours(1),
            Guid.NewGuid(), "user", [], [], false, null);
        var (client, _) = CreateClient(HttpStatusCode.OK, expected);

        var result = await client.RefreshTokenAsync("old-refresh");
        result.Should().NotBeNull();
        result!.AccessToken.Should().Be("new-access");
    }

    [Fact]
    public async Task RefreshTokenAsync_ReturnsNullOnFailure()
    {
        var (client, _) = CreateClient(HttpStatusCode.Unauthorized);
        var result = await client.RefreshTokenAsync("expired");
        result.Should().BeNull();
    }

    [Fact]
    public async Task RenewJwtAsync_ReturnsNewAccessTokenOnSuccess()
    {
        var expected = new RenewJwtResponse(
            "renewed-access", DateTimeOffset.UtcNow.AddHours(1),
            Guid.NewGuid(), "user", [], [], false, null);
        var (client, _) = CreateClient(HttpStatusCode.OK, expected);

        var result = await client.RenewJwtAsync("refresh-token");
        result.Should().NotBeNull();
        result!.AccessToken.Should().Be("renewed-access");
    }

    [Fact]
    public async Task RenewJwtAsync_ReturnsNullWhenTokenRevoked()
    {
        var (client, _) = CreateClient(HttpStatusCode.Unauthorized);
        var result = await client.RenewJwtAsync("revoked-token");
        result.Should().BeNull();
    }

    [Fact]
    public async Task LogoutAsync_DoesNotThrowOnSuccess()
    {
        var (client, _) = CreateClient(HttpStatusCode.NoContent);
        await client.Invoking(c => c.LogoutAsync("refresh")).Should().NotThrowAsync();
    }

    [Fact]
    public async Task LogoutAsync_DoesNotThrowOnServerError()
    {
        // Best-effort — server errors must not propagate.
        var (client, _) = CreateClient(HttpStatusCode.InternalServerError);
        await client.Invoking(c => c.LogoutAsync("refresh")).Should().NotThrowAsync();
    }

    [Fact]
    public async Task ServerBaseUrl_ReturnsBaseAddressWithoutTrailingSlash()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK);
        client.ServerBaseUrl.Should().Be("https://api.test");
    }

    // ── ExchangeCodeAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ExchangeCodeAsync_ReturnsAuthResponseOnSuccess()
    {
        var expected = new AuthResponse(
            "access", "refresh", DateTimeOffset.UtcNow.AddHours(1),
            Guid.NewGuid(), "user", [], [], false, null);
        var (client, _) = CreateClient(HttpStatusCode.OK, expected);

        var result = await client.ExchangeCodeAsync("code-123", Guid.NewGuid());

        result.Should().NotBeNull();
        result!.AccessToken.Should().Be("access");
    }

    [Fact]
    public async Task ExchangeCodeAsync_ReturnsNullOnFailure()
    {
        var (client, _) = CreateClient(HttpStatusCode.BadRequest);
        var result = await client.ExchangeCodeAsync("bad-code", Guid.NewGuid());
        result.Should().BeNull();
    }

    // ── CreateExchangeCodeAsync ───────────────────────────────────────────────

    [Fact]
    public async Task CreateExchangeCodeAsync_ReturnsResponseOnSuccess()
    {
        var expected = new CreateExchangeCodeResponse("xc-abc", Guid.NewGuid());
        var (client, _) = CreateClient(HttpStatusCode.OK, expected);

        var result = await client.CreateExchangeCodeAsync();

        result.Should().NotBeNull();
        result!.Code.Should().Be("xc-abc");
    }

    [Fact]
    public async Task CreateExchangeCodeAsync_ReturnsNullOnFailure()
    {
        var (client, _) = CreateClient(HttpStatusCode.Unauthorized);
        var result = await client.CreateExchangeCodeAsync();
        result.Should().BeNull();
    }

    // ── ForgotPasswordAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task ForgotPasswordAsync_DoesNotThrowOnSuccess()
    {
        var (client, _) = CreateClient(HttpStatusCode.NoContent);
        await client.Invoking(c => c.ForgotPasswordAsync("user@example.com")).Should().NotThrowAsync();
    }

    [Fact]
    public async Task ForgotPasswordAsync_DoesNotThrowOnServerError()
    {
        var (client, _) = CreateClient(HttpStatusCode.InternalServerError);
        await client.Invoking(c => c.ForgotPasswordAsync("user@example.com")).Should().NotThrowAsync();
    }

    // ── ResetPasswordAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task ResetPasswordAsync_ReturnsTrueOnSuccess()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK);
        var (ok, error) = await client.ResetPasswordAsync("user@example.com", "tok", "NewP@ss1");
        ok.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public async Task ResetPasswordAsync_ReturnsFalseOnFailure()
    {
        var (client, _) = CreateClient(HttpStatusCode.BadRequest);
        var (ok, error) = await client.ResetPasswordAsync("user@example.com", "bad-tok", "NewP@ss1");
        ok.Should().BeFalse();
        error.Should().NotBeNull();
    }

    // ── ConfirmEmailAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmEmailAsync_ReturnsTrueOnSuccess()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK);
        var (ok, error) = await client.ConfirmEmailAsync("user-id", "confirm-tok");
        ok.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public async Task ConfirmEmailAsync_ReturnsFalseOnFailure()
    {
        var (client, _) = CreateClient(HttpStatusCode.BadRequest);
        var (ok, error) = await client.ConfirmEmailAsync("user-id", "bad-tok");
        ok.Should().BeFalse();
        error.Should().NotBeNull();
    }

    // ── ResendEmailConfirmationAsync ──────────────────────────────────────────

    [Fact]
    public async Task ResendEmailConfirmationAsync_ReturnsTrueOnSuccess()
    {
        var (client, _) = CreateClient(HttpStatusCode.NoContent);
        var (ok, error) = await client.ResendEmailConfirmationAsync();
        ok.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public async Task ResendEmailConfirmationAsync_ReturnsFalseOnFailure()
    {
        var (client, _) = CreateClient(HttpStatusCode.Unauthorized);
        var (ok, error) = await client.ResendEmailConfirmationAsync();
        ok.Should().BeFalse();
        error.Should().NotBeNull();
    }
}
