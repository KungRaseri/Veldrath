using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Veldrath.Client.HostedWeb;
using Xunit;

namespace Veldrath.Client.Tests.HostedWeb;

/// <summary>
/// Integration tests for <see cref="HostedGameServer"/>.  These tests start a real
/// embedded ASP.NET Core server on a random localhost port and verify that it hosts
/// the Blazor game UI correctly.
/// </summary>
/// <remarks>
/// Tests in this class use a shared <see cref="HostedGameServer"/> instance via
/// <see cref="IAsyncLifetime"/> to avoid starting/stopping the server per test.
/// Each test uses a fresh <see cref="HttpClient"/> pointed at the server.
/// </remarks>
public sealed class HostedGameServerIntegrationTests : IAsyncLifetime
{
    private readonly HostedGameServer _server;
    private readonly HttpClient _httpClient;
    private readonly ILogger<HostedGameServerIntegrationTests> _logger;

    /// <summary>
    /// Initializes a new instance, creating a <see cref="HostedGameServer"/> that
    /// connects to a placeholder remote URL (the tests only verify the embedded
    /// server's self-hosted endpoints, not the remote game server).
    /// </summary>
    public HostedGameServerIntegrationTests()
    {
        _logger = NullLogger<HostedGameServerIntegrationTests>.Instance;

        // Use a logger factory that writes to the test output via XUnit.
        // NullLoggerFactory is safe for this test since HostedGameServer
        // only uses the factory for its own logging pipeline.
        var loggerFactory = NullLoggerFactory.Instance;

        // The remote server URL is a placeholder; integration tests only exercise
        // the embedded server's own endpoints (/health, /), not proxied requests.
        _server = new HostedGameServer("http://localhost:9000", loggerFactory);
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _server.StartAsync();
        _logger.LogInformation("HostedGameServer started on port {Port}.", _server.Port);
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await _server.StopAsync();
        _httpClient.Dispose();
    }

    /// <summary>
    /// Verifies that the server starts and is assigned a random port.
    /// </summary>
    [Fact]
    public void Server_Starts_And_Assigns_Port()
    {
        Assert.True(_server.IsRunning);
        Assert.True(_server.Port > 0, "Server port should be greater than 0.");
        Assert.NotNull(_server.BaseUrl);
        Assert.Contains("localhost", _server.BaseUrl, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies the health endpoint returns 200 OK with a healthy status.
    /// </summary>
    [Fact]
    public async Task Health_Endpoint_Returns_200_OK()
    {
        var response = await _httpClient.GetAsync($"{_server.BaseUrl}/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(body);
        Assert.Equal("healthy", body.Status);
    }

    /// <summary>
    /// Verifies that the root URL returns an HTML page with Blazor Server scripts,
    /// confirming the Blazor circuit can be established.
    /// </summary>
    [Fact]
    public async Task Root_Returns_Blazor_Html()
    {
        var response = await _httpClient.GetAsync(_server.BaseUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        var html = await response.Content.ReadAsStringAsync();

        // The page should contain Blazor Server framework script references.
        Assert.Contains("_framework/blazor.web.js", html);

        // The page should contain the EmbeddedRoutes component.
        Assert.Contains("Veldrath.GameClient.Components", html);

        // The bridge.js script should be referenced.
        Assert.Contains("bridge.js", html);
    }

    /// <summary>
    /// Verifies that the server binds to 127.0.0.1 only (the BaseUrl should be localhost).
    /// </summary>
    [Fact]
    public void Server_Binds_To_Localhost_Only()
    {
        Assert.NotNull(_server.BaseUrl);
        var uri = new Uri(_server.BaseUrl);

        // The host should be "localhost" or "127.0.0.1".
        Assert.True(
            uri.Host == "localhost" || uri.Host == "127.0.0.1",
            $"Expected localhost or 127.0.0.1, got '{uri.Host}'.");

        // The scheme should be HTTP (not HTTPS).
        Assert.Equal("http", uri.Scheme);
    }

    /// <summary>
    /// Verifies that the server stops gracefully and no longer responds after shutdown.
    /// </summary>
    [Fact]
    public async Task Server_Stops_Gracefully()
    {
        // Arrange: start a separate server instance for this test.
        var loggerFactory = NullLoggerFactory.Instance;
        var testServer = new HostedGameServer("http://localhost:9000", loggerFactory);
        await testServer.StartAsync();
        Assert.True(testServer.IsRunning);

        // Act: stop the server.
        await testServer.StopAsync();

        // Assert: the server should report as not running.
        Assert.False(testServer.IsRunning);
        Assert.Equal(0, testServer.Port);
        Assert.Null(testServer.BaseUrl);

        // HTTP requests should fail with a connection refused exception.
        // We use a very short timeout to avoid hanging.
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var ex = await Record.ExceptionAsync(() =>
            client.GetAsync($"http://localhost:{testServer.Port}/health"));

        Assert.NotNull(ex);
    }

    /// <summary>
    /// Verifies that the server respects CancellationToken during startup
    /// and shutdown.
    /// </summary>
    [Fact]
    public async Task Server_Respects_CancellationToken()
    {
        using var cts = new CancellationTokenSource();

        // Start the server normally.
        var loggerFactory = NullLoggerFactory.Instance;
        var testServer = new HostedGameServer("http://localhost:9000", loggerFactory);
        await testServer.StartAsync(cts.Token);
        Assert.True(testServer.IsRunning);

        // Cancel and stop.
        await cts.CancelAsync();

        // Stopping with a cancelled token should still work (best-effort).
        await testServer.StopAsync(cts.Token);
        Assert.False(testServer.IsRunning);
    }

    /// <summary>
    /// DTO for the health endpoint response.
    /// </summary>
    private sealed record HealthResponse(string Status)
    {
        /// <summary>The server status string (e.g. "healthy").</summary>
        public string Status { get; init; } = Status;
    }
}
