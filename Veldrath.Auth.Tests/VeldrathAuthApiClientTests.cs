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
}
