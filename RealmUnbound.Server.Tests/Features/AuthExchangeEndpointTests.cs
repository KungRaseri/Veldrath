using System.Net;
using System.Net.Http.Json;
using RealmUnbound.Server.Tests.Infrastructure;

namespace RealmUnbound.Server.Tests.Features;

[Trait("Category", "Integration")]
public class AuthExchangeEndpointTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> GetTokenAsync(string username)
    {
        var email = $"{username.ToLower()}@test.com";
        await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = email, Username = username, Password = "Pass1234!" });
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new { Email = email, Password = "Pass1234!" });
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
            new ExchangeCodeRequest(""));

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
            new ExchangeCodeRequest("0000000000000000000000000000000000000000000000000000000000000000"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
