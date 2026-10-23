using System.Net;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// CORS regression tests for browser SignalR clients. The negotiate preflight sends
/// <c>x-signalr-user-agent</c>, which the original policy rejected.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class CorsTests(WebAppFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task SignalR_Negotiate_Preflight_Allows_SignalR_User_Agent_Header()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/hubs/game/negotiate");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "x-signalr-user-agent,authorization");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var headerDump = string.Join(
            " | ",
            response.Headers.Select(header => $"{header.Key}: {string.Join(',', header.Value)}"));

        var allowedHeaders = response.Headers
            .GetValues("Access-Control-Allow-Headers")
            .SelectMany(value => value.Split(',', StringSplitOptions.TrimEntries))
            .ToList();

        allowedHeaders.Any(header =>
                string.Equals(header, "x-signalr-user-agent", StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue(headerDump);
        allowedHeaders.Any(header =>
                string.Equals(header, "authorization", StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue(headerDump);
    }

    [Fact]
    public async Task Preflight_From_Disallowed_Origin_Is_Rejected()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/hubs/game/negotiate");
        request.Headers.Add("Origin", "https://evil.example");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await _client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }
}
