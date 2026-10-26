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

/// <summary>
/// Pins the CORS defaults the browser needs. These are the ones that would otherwise only break in a browser:
/// the Vite dev origin, the preview origin the Playwright game-loop spec runs against, and the write verbs the
/// Foundry content editor uses.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class CorsDefaultTests(WebAppFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<HttpResponseMessage> PreflightAsync(string path, string origin, string method)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        return await _client.SendAsync(request);
    }

    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("http://localhost:4173")]
    [InlineData("https://localhost:5173")]
    [InlineData("http://localhost:8081")]
    public async Task The_default_dev_and_preview_origins_are_allowed(string origin)
    {
        var response = await PreflightAsync("/api/content/item", origin, "GET");

        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Contain(origin);
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("POST")]
    public async Task The_write_verbs_the_content_editor_uses_survive_a_preflight(string method)
    {
        // Foundry edits content from the browser, so PUT and DELETE are now preflighted requests. Dropping them from
        // the policy would fail the save with an opaque CORS error, not a 4xx the editor could explain.
        var response = await PreflightAsync("/api/content/item", "http://localhost:5173", method);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Methods")
            .SelectMany(value => value.Split(',', StringSplitOptions.TrimEntries))
            .Should().Contain(method);
    }

    [Fact]
    public async Task The_policy_does_not_allow_every_method()
    {
        // An explicit allow-list keeps TRACE and CONNECT out; AllowAnyMethod would quietly let them back in. The
        // middleware still echoes the origin, so the methods header is what has to be asserted.
        var response = await PreflightAsync("/api/content/item", "http://localhost:5173", "TRACE");

        var allowedMethods = response.Headers
            .GetValues("Access-Control-Allow-Methods")
            .SelectMany(value => value.Split(',', StringSplitOptions.TrimEntries))
            .ToList();

        allowedMethods.Should().NotContain("TRACE");
        allowedMethods.Should().NotContain("CONNECT");
        allowedMethods.Should().Contain(["GET", "POST", "PUT", "DELETE"]);
    }
}
