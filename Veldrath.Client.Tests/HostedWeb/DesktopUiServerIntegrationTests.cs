using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Veldrath.Client.HostedWeb;
using Veldrath.Client.Services;
using Xunit;

namespace Veldrath.Client.Tests.HostedWeb;

/// <summary>
/// Integration tests for <see cref="DesktopUiServer"/>. They start a real loopback server on a
/// random port and verify static Svelte hosting, SPA fallback, health reporting, token-injecting
/// API proxying, and graceful shutdown.
/// </summary>
public sealed class DesktopUiServerIntegrationTests : IAsyncLifetime
{
    private readonly string _uiDir =
        Path.Combine(Path.GetTempPath(), $"veldrath-desktop-ui-{Guid.NewGuid():N}");

    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(10) };
    private WebApplication? _mockServer;
    private DesktopUiServer? _server;
    private string _mockUrl = string.Empty;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_uiDir);
        await File.WriteAllTextAsync(
            Path.Combine(_uiDir, "index.html"),
            "<!doctype html><html><body><div id=\"svelte-desktop\">desktop-ui</div></body></html>");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Production",
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));

        var mock = builder.Build();
        mock.MapGet("/api/characters", (HttpContext context) =>
            context.Request.Headers.Authorization == "Bearer test-token"
                ? Results.Json(new[]
                {
                    new { id = Guid.NewGuid(), name = "Aria", level = 7, className = "Fighter" },
                })
                : Results.Unauthorized());
        await mock.StartAsync();
        _mockServer = mock;
        _mockUrl = mock.Urls.First();

        _server = new DesktopUiServer(
            _mockUrl,
            LoggerFactory.Create(_ => { }),
            new TokenStore(),
            () => Task.FromResult<string?>("test-token"),
            _uiDir);

        await _server.StartAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (_server is not null)
        {
            await _server.StopAsync();
            await _server.DisposeAsync();
        }

        if (_mockServer is not null)
        {
            await _mockServer.StopAsync();
            await _mockServer.DisposeAsync();
        }

        _client.Dispose();

        try
        {
            Directory.Delete(_uiDir, recursive: true);
        }
        catch
        {
            // Best-effort temp cleanup.
        }
    }

    [Fact]
    public void Server_Starts_On_Loopback_With_Random_Port()
    {
        Assert.True(_server!.IsRunning);
        Assert.True(_server.Port > 0);
        Assert.NotNull(_server.BaseUrl);

        var uri = new Uri(_server.BaseUrl!);
        Assert.Equal("127.0.0.1", uri.Host);
        Assert.Equal("http", uri.Scheme);
    }

    [Fact]
    public async Task Health_Endpoint_Returns_Healthy()
    {
        var response = await _client.GetAsync($"{_server!.BaseUrl}/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("healthy", body!.Status);
    }

    [Fact]
    public async Task Root_Serves_The_Static_Svelte_Index()
    {
        var response = await _client.GetAsync(_server!.BaseUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("svelte-desktop", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Client_Routes_Fall_Back_To_Index()
    {
        var response = await _client.GetAsync($"{_server!.BaseUrl}/desktop");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("svelte-desktop", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Api_Proxy_Injects_The_Desktop_Token()
    {
        var response = await _client.GetAsync($"{_server!.BaseUrl}/api/characters");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var characters = await response.Content.ReadFromJsonAsync<CharacterResponse[]>();
        Assert.Single(characters!);
        Assert.Equal("Aria", characters![0].Name);
    }

    [Fact]
    public async Task Api_Proxy_Returns_401_When_Unauthenticated()
    {
        await using var unauthenticated = new DesktopUiServer(
            _mockUrl,
            LoggerFactory.Create(_ => { }),
            new TokenStore(),
            () => Task.FromResult<string?>(null),
            _uiDir);
        await unauthenticated.StartAsync();

        var response = await _client.GetAsync($"{unauthenticated.BaseUrl}/api/characters");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await unauthenticated.StopAsync();
    }

    [Fact]
    public async Task Server_Stops_Gracefully()
    {
        await _server!.StopAsync();

        Assert.False(_server.IsRunning);
        Assert.Equal(0, _server.Port);
        Assert.Null(_server.BaseUrl);
    }

    private sealed record HealthResponse(string Status);

    private sealed record CharacterResponse(Guid Id, string Name, int Level, string ClassName);
}
