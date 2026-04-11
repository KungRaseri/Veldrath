using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

[Trait("Category", "Integration")]
public class ReportEndpointTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(string Token, string Username)> RegisterAsync(string username)
    {
        var email = $"{username.ToLower()}@test.com";
        await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = email, Username = username, Password = "TestP@ssword123" });
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new { Email = email, Password = "TestP@ssword123" });
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return (auth!.AccessToken, username);
    }

    [Fact]
    public async Task SubmitReport_Unauthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/reports",
            new SubmitReportRequest("someuser", "spamming"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SubmitReport_UnknownTarget_ReturnsBadRequest()
    {
        // Arrange
        var (token, _) = await RegisterAsync("report_reporter1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsJsonAsync("/api/reports",
            new SubmitReportRequest("totally_nonexistent_player", "griefing"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SubmitReport_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var (reporterToken, _) = await RegisterAsync("report_reporter2");
        await RegisterAsync("report_target2");           // just needs to exist

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reporterToken);

        // Act
        var response = await client.PostAsJsonAsync("/api/reports",
            new SubmitReportRequest("report_target2", "verbal abuse"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task SubmitReport_EmptyReason_ReturnsBadRequest()
    {
        // Arrange
        var (token, _) = await RegisterAsync("report_reporter3");
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.PostAsJsonAsync("/api/reports",
            new SubmitReportRequest("report_reporter3", ""));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
