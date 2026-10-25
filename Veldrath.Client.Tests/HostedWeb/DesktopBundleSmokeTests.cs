using System.Net;
using Microsoft.Extensions.Logging;
using Veldrath.Client.HostedWeb;
using Veldrath.Client.Services;
using Xunit;
using Xunit.Abstractions;

namespace Veldrath.Client.Tests.HostedWeb;

/// <summary>
/// Smoke tests the <em>real</em> Svelte desktop bundle (<c>web/client/build-desktop</c>) through
/// <see cref="DesktopUiServer"/>, closing the last manual gate of the SvelteKit migration: the
/// WebView2 shell always pointed at a bundle that only ever got built, never served.
/// <para>
/// CI builds the bundle with <c>npm run build:desktop</c> before running the client tests, so
/// these run there. Locally they no-op when the bundle is absent — build it first to exercise them:
/// <c>cd web && npm run build:desktop</c>.
/// </para>
/// </summary>
[Trait("Category", "Bundle")]
public sealed class DesktopBundleSmokeTests(ITestOutputHelper output) : IAsyncLifetime
{
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(15) };
    private DesktopUiServer? _server;
    private string? _bundlePath;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        _bundlePath = FindBundle();
        if (_bundlePath is null)
        {
            output.WriteLine("Svelte desktop bundle not found; run 'npm run build:desktop' in web/.");
            return;
        }

        _server = new DesktopUiServer(
            "http://127.0.0.1:1", // never contacted by these assertions
            LoggerFactory.Create(_ => { }),
            new TokenStore(),
            () => Task.FromResult<string?>(null),
            _bundlePath);

        await _server.StartAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        _client.Dispose();
        if (_server is not null)
        {
            await _server.StopAsync();
            await _server.DisposeAsync();
        }
    }

    /// <summary>The bundle serves the SPA shell and its immutable assets from disk.</summary>
    [Fact]
    public async Task ServesIndexAndImmutableAssets()
    {
        if (_server is null) return;

        var index = await _client.GetAsync(new Uri(new Uri(_server.BaseUrl!), "/"));
        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
        var html = await index.Content.ReadAsStringAsync();
        Assert.Contains("<div", html, StringComparison.OrdinalIgnoreCase);

        // adapter-static emits hashed module assets under /_app/immutable/.
        var assetPath = FirstImmutableAsset(html);
        Assert.NotNull(assetPath);

        var asset = await _client.GetAsync(new Uri(new Uri(_server.BaseUrl!), assetPath));
        Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
        Assert.Contains("javascript", asset.Content.Headers.ContentType?.MediaType ?? string.Empty);
        Assert.NotEmpty(await asset.Content.ReadAsByteArrayAsync());
    }

    /// <summary>Client-side routes fall back to the SPA shell instead of 404ing.</summary>
    [Fact]
    public async Task FallsBackToSpaForClientRoutes()
    {
        if (_server is null) return;

        var response = await _client.GetAsync(new Uri(new Uri(_server.BaseUrl!), "/desktop"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<div", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The loopback server reports healthy for the desktop host's readiness check.</summary>
    [Fact]
    public async Task ReportsHealth()
    {
        if (_server is null) return;

        var response = await _client.GetAsync(new Uri(new Uri(_server.BaseUrl!), "/health"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// The WebView2 runtime loader must ship with the client, otherwise the shell cannot host the
    /// SPA on a machine without a system-wide install.
    /// </summary>
    [Fact]
    public void ShipsWebView2RuntimeLoader()
    {
        var loader = Directory
            .EnumerateFiles(AppContext.BaseDirectory, "WebView2Loader.dll", SearchOption.AllDirectories)
            .FirstOrDefault();

        Assert.False(loader is null, "WebView2Loader.dll is missing from the client output.");
    }

    private static string? FirstImmutableAsset(string html)
    {
        const string marker = "/_app/immutable/";
        var index = html.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0) return null;

        var end = html.IndexOfAny(['"', '\''], index);
        return end < 0 ? null : html[index..end];
    }

    private static string? FindBundle()
    {
        var configured = Environment.GetEnvironmentVariable("VELDRATH_DESKTOP_UI");
        if (!string.IsNullOrWhiteSpace(configured) && LooksLikeBundle(configured))
        {
            return Path.GetFullPath(configured);
        }

        // Walk up from the test output (…/Veldrath.Client.Tests/bin/<cfg>/<tfm>) to the repo root.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "web", "client", "build-desktop");
            if (LooksLikeBundle(candidate)) return candidate;
            directory = directory.Parent;
        }

        return null;
    }

    private static bool LooksLikeBundle(string path) => File.Exists(Path.Combine(path, "index.html"));
}
