using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Veldrath.Server.Features.Auth;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

[Trait("Category", "Integration")]
public class AuthExchangeEndpointTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    private readonly HttpClient               _client      = factory.CreateClient();
    private readonly AuthExchangeCodeService  _exchangeSvc = factory.Services.GetRequiredService<AuthExchangeCodeService>();

    private async Task<string> GetTokenAsync(string username)
    {
        var email = $"{username.ToLower()}@test.com";
        await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = email, Username = username, Password = "TestP@ssword123" });
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new { Email = email, Password = "TestP@ssword123" });
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.AccessToken;
    }

    [Fact]
    public async Task Exchange_MissingCode_ReturnsBadRequest()
    {
        // Arrange
        await GetTokenAsync("exch_user1");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/exchange",
            new ExchangeCodeRequest("", Guid.NewGuid()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Exchange_InvalidCode_ReturnsBadRequest()
    {
        // Arrange
        await GetTokenAsync("exch_user2");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/exchange",
            new ExchangeCodeRequest("0000000000000000000000000000000000000000000000000000000000000000", Guid.NewGuid()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Exchange_ValidCode_ReturnsOkWithAuthResponse()
    {
        // Arrange — mint a code directly via the singleton service (no browser/OAuth needed)
        var accountId = Guid.NewGuid();
        var fakeResponse = new AuthResponse(
            "fake-access-token",
            "fake-refresh-token",
            DateTimeOffset.UtcNow.AddMinutes(15),
            accountId,
            "ExchangeUser",
            [],
            []);
        var code = _exchangeSvc.CreateCode(fakeResponse, accountId);

        // Act
        var httpResponse = await _client.PostAsJsonAsync("/api/auth/exchange",
            new ExchangeCodeRequest(code, accountId));

        // Assert
        httpResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await httpResponse.Content.ReadFromJsonAsync<AuthResponse>();
        result.Should().NotBeNull();
        result!.AccountId.Should().Be(accountId);
        result.Username.Should().Be("ExchangeUser");
    }

    [Fact]
    public async Task Exchange_ValidCode_CanOnlyBeRedeemedOnce()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var fakeResponse = new AuthResponse(
            "fake-access-token-2",
            "fake-refresh-token-2",
            DateTimeOffset.UtcNow.AddMinutes(15),
            accountId,
            "OnceOnlyUser",
            [],
            []);
        var code = _exchangeSvc.CreateCode(fakeResponse, accountId);
        var request = new ExchangeCodeRequest(code, accountId);

        // Act — first redemption should succeed, second should fail
        var first  = await _client.PostAsJsonAsync("/api/auth/exchange", request);
        var second = await _client.PostAsJsonAsync("/api/auth/exchange", request);

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
