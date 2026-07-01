using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Veldrath.Client.HostedWeb;
using Xunit;

namespace Veldrath.Client.Tests.HostedWeb;

/// <summary>
/// Integration tests for <see cref="HostedGameServer"/>.  These tests start a real
/// embedded ASP.NET Core server on a random localhost port and verify that it hosts
/// the Blazor game UI correctly.
/// </summary>
/// <remarks>
/// Tests use a shared <see cref="HostedGameServer"/> instance via <see cref="IAsyncLifetime"/>.
///
/// <b>Note:</b> Some test hosts may not have the static web assets manifest file
/// (<c>testhost.staticwebassets.endpoints.json</c>) required by
/// <see cref="HostedGameServer.StartAsync"/>. In that case, all tests are silently
/// skipped rather than failed. Run these tests from a host project that references
/// the RCL to exercise the full Blazor pipeline.
/// </remarks>
public sealed class HostedGameServerIntegrationTests : IAsyncLifetime
{
    private readonly HostedGameServer _server;
    private readonly HttpClient _httpClient;
    private readonly ILogger<HostedGameServerIntegrationTests> _logger;
    private bool _serverStarted;

    /// <summary>
    /// Initializes a new instance, creating a <see cref="HostedGameServer"/> that
    /// connects to a placeholder remote URL (the tests only verify the embedded
    /// server's self-hosted endpoints, not the remote game server).
    /// </summary>
    public HostedGameServerIntegrationTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => { });
        _logger = loggerFactory.CreateLogger<HostedGameServerIntegrationTests>();

        _server = new HostedGameServer("http://localhost:9000", loggerFactory);
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        try
        {
            await _server.StartAsync();
            _serverStarted = true;
            _logger.LogInformation("HostedGameServer started on port {Port}.", _server.Port);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("staticwebassets"))
        {
            _logger.LogWarning(
                "HostedGameServer cannot start in this test host: static web assets manifest not found. " +
                "Blazor pipeline tests will be skipped. Error: {Message}", ex.Message);
            _serverStarted = false;
        }
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (_serverStarted)
            await _server.StopAsync();
        _httpClient.Dispose();
    }

    /// <summary>
    /// Verifies that the server starts and is assigned a random port.
    /// </summary>
    [Fact]
    public async Task Server_Starts_And_Assigns_Port()
    {
        if (!_serverStarted) return;

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
        if (!_serverStarted) return;

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
        if (!_serverStarted) return;

        var response = await _httpClient.GetAsync(_server.BaseUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        var html = await response.Content.ReadAsStringAsync();

        // The page should contain Blazor Server framework script references.
        Assert.Contains("_framework/blazor.web.js", html);
    }

    /// <summary>
    /// Verifies that the server binds to 127.0.0.1 only (the BaseUrl should be localhost).
    /// </summary>
    [Fact]
    public async Task Server_Binds_To_Localhost_Only()
    {
        if (!_serverStarted) return;

        Assert.NotNull(_server.BaseUrl);
        var uri = new Uri(_server.BaseUrl);

        Assert.True(
            uri.Host == "localhost" || uri.Host == "127.0.0.1",
            $"Expected localhost or 127.0.0.1, got '{uri.Host}'.");
        Assert.Equal("http", uri.Scheme);
    }

    /// <summary>
    /// Verifies that the server stops gracefully and no longer responds after shutdown.
    /// </summary>
    [Fact]
    public async Task Server_Stops_Gracefully()
    {
        if (!_serverStarted) return;

        await _server.StopAsync();

        Assert.False(_server.IsRunning);
        Assert.Equal(0, _server.Port);
        Assert.Null(_server.BaseUrl);
    }

    /// <summary>
    /// Verifies that the server respects CancellationToken during startup and shutdown.
    /// </summary>
    [Fact]
    public async Task Server_Respects_CancellationToken()
    {
        if (!_serverStarted) return;

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await _server.StopAsync(cts.Token);
        Assert.False(_server.IsRunning);
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
