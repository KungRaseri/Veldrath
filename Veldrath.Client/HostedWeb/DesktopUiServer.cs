using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Veldrath.Client.Services;

namespace Veldrath.Client.HostedWeb;

/// <summary>
/// Serves the static Svelte desktop build to the Avalonia WebView2 shell and proxies
/// <c>/api</c> and <c>/hubs</c> to the remote Veldrath.Server.
/// <para>
/// The proxy injects the desktop client's JWT server-side, so the WebView never holds a
/// token and every request is same-origin. The server binds to <c>127.0.0.1</c> on a
/// random port so it is unreachable from other machines.
/// </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class DesktopUiServer : IHostedGameServer, IAsyncDisposable
{
    private const int WebSocketBufferSize = 16 * 1024;

    private static readonly string[] ProxyMethods =
        ["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"];

    private static readonly HashSet<string> HopByHopHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection", "Keep-Alive", "Proxy-Authenticate", "Proxy-Authorization",
        "TE", "Trailer", "Transfer-Encoding", "Upgrade",
    };

    private readonly ILogger<DesktopUiServer> _logger;
    private readonly string _remoteServerUrl;
    private readonly Func<Task<string?>> _accessTokenProvider;
    private readonly string? _uiPathOverride;
    private readonly HttpClient _proxyClient = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        AutomaticDecompression = DecompressionMethods.None,
    });

    private PhysicalFileProvider? _fileProvider;
    private WebApplication? _app;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DesktopUiServer"/> class.
    /// </summary>
    /// <param name="remoteServerUrl">Base URL of the remote Veldrath.Server (e.g. <c>http://localhost:9000</c>).</param>
    /// <param name="loggerFactory">Logger factory for the local server pipeline.</param>
    /// <param name="tokenStore">Desktop token store used as the default access-token source.</param>
    /// <param name="accessTokenProvider">
    /// Optional token provider that refreshes the desktop session before proxying. When omitted,
    /// the current <see cref="TokenStore.AccessToken"/> is used.
    /// </param>
    /// <param name="uiPathOverride">Optional path to the static Svelte build (used by tests).</param>
    public DesktopUiServer(
        string remoteServerUrl,
        ILoggerFactory loggerFactory,
        TokenStore tokenStore,
        Func<Task<string?>>? accessTokenProvider = null,
        string? uiPathOverride = null)
    {
        _remoteServerUrl = remoteServerUrl.TrimEnd('/');
        _logger = loggerFactory.CreateLogger<DesktopUiServer>();
        _accessTokenProvider = accessTokenProvider
            ?? (() => Task.FromResult(tokenStore.AccessToken));
        _uiPathOverride = uiPathOverride;
    }

    /// <inheritdoc />
    public int Port { get; private set; }

    /// <inheritdoc />
    public bool IsRunning => _app is not null;

    /// <inheritdoc />
    public string? BaseUrl => IsRunning ? $"http://127.0.0.1:{Port}" : null;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_app is not null)
        {
            _logger.LogWarning("DesktopUiServer is already running on port {Port}.", Port);
            return;
        }

        var uiPath = ResolveUiPath();
        if (uiPath is null)
        {
            throw new InvalidOperationException(
                "The Svelte desktop build was not found. Run 'npm run build:desktop' in web/client, " +
                "publish the SvelteApp folder next to the client, or set VELDRATH_DESKTOP_UI.");
        }

        _logger.LogInformation("Starting desktop UI server from {UiPath}...", uiPath);
        _fileProvider = new PhysicalFileProvider(uiPath);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Production",
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, 0);
        });

        var app = builder.Build();

        app.UseWebSockets();
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = _fileProvider });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = _fileProvider });

        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
        app.MapMethods("/api/{**path}", ProxyMethods, ProxyAsync);
        app.MapMethods("/hubs/{**path}", ProxyMethods, ProxyAsync);
        app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = _fileProvider });

        _app = app;
        await app.StartAsync(ct);

        var address = app.Urls.FirstOrDefault();
        if (address is not null && Uri.TryCreate(address, UriKind.Absolute, out var uri))
        {
            Port = uri.Port;
            _logger.LogInformation(
                "DesktopUiServer started on {BaseUrl} (remote server: {RemoteUrl}).",
                BaseUrl, _remoteServerUrl);
        }
        else
        {
            _logger.LogError("DesktopUiServer started but no bound address was assigned.");
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_app is null)
            return;

        _logger.LogInformation("Stopping desktop UI server...");

        try
        {
            await _app.StopAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while stopping DesktopUiServer.");
        }

        await _app.DisposeAsync();
        _app = null;
        Port = 0;

        _fileProvider?.Dispose();
        _fileProvider = null;

        _logger.LogInformation("DesktopUiServer stopped.");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await StopAsync();
        _proxyClient.Dispose();
        GC.SuppressFinalize(this);
    }

    private Task ProxyAsync(HttpContext context)
    {
        if (context.WebSockets.IsWebSocketRequest)
            return ProxyWebSocketAsync(context);

        return ProxyHttpAsync(context);
    }

    private async Task ProxyHttpAsync(HttpContext context)
    {
        var token = await _accessTokenProvider();
        if (string.IsNullOrWhiteSpace(token))
        {
            await WriteUnauthorizedAsync(context);
            return;
        }

        var target = $"{_remoteServerUrl}{context.Request.Path}{context.Request.QueryString}";
        using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), target);
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");

        if (context.Request.ContentLength is > 0 ||
            context.Request.Headers.ContainsKey("Transfer-Encoding"))
        {
            request.Content = new StreamContent(context.Request.Body);
            if (context.Request.ContentType is { Length: > 0 } contentType)
            {
                request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
            }
        }

        foreach (var header in context.Request.Headers)
        {
            if (HopByHopHeaders.Contains(header.Key) ||
                header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) ||
                header.Key.StartsWith("Sec-WebSocket", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var values = header.Value.ToArray();
            if (!request.Headers.TryAddWithoutValidation(header.Key, values))
            {
                request.Content?.Headers.TryAddWithoutValidation(header.Key, values);
            }
        }

        try
        {
            using var response = await _proxyClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);

            context.Response.StatusCode = (int)response.StatusCode;

            foreach (var header in response.Headers.Concat(response.Content.Headers))
            {
                if (HopByHopHeaders.Contains(header.Key) ||
                    header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected.
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Proxy request to {Target} failed.", target);
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status502BadGateway;
            }
        }
    }

    private async Task ProxyWebSocketAsync(HttpContext context)
    {
        var token = await _accessTokenProvider();
        if (string.IsNullOrWhiteSpace(token))
        {
            await WriteUnauthorizedAsync(context);
            return;
        }

        if (!Uri.TryCreate(_remoteServerUrl, UriKind.Absolute, out var remoteUri))
        {
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            return;
        }

        var scheme = remoteUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
        var target = new Uri(
            $"{scheme}://{remoteUri.Authority}{context.Request.Path}{context.Request.QueryString}");

        using var upstream = new ClientWebSocket();
        upstream.Options.SetRequestHeader("Authorization", $"Bearer {token}");

        using var downstream = await context.WebSockets.AcceptWebSocketAsync();

        try
        {
            await upstream.ConnectAsync(target, context.RequestAborted);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            var toUpstream = PumpAsync(downstream, upstream, cts.Token);
            var toDownstream = PumpAsync(upstream, downstream, cts.Token);

            await Task.WhenAny(toUpstream, toDownstream);
            cts.Cancel();
            await Task.WhenAll(toUpstream, toDownstream);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception ex) when (ex is WebSocketException or HttpRequestException)
        {
            _logger.LogDebug(ex, "WebSocket proxy to {Target} closed.", target);
        }
    }

    private static async Task PumpAsync(WebSocket source, WebSocket destination, CancellationToken ct)
    {
        var buffer = new byte[WebSocketBufferSize];

        try
        {
            while (!ct.IsCancellationRequested && source.State == WebSocketState.Open)
            {
                var result = await source.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await destination.CloseOutputAsync(
                        result.CloseStatus ?? WebSocketCloseStatus.NormalClosure,
                        result.CloseStatusDescription,
                        ct);
                    return;
                }

                await destination.SendAsync(
                    buffer.AsMemory(0, result.Count),
                    result.MessageType,
                    result.EndOfMessage,
                    ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException)
        {
        }
    }

    private static Task WriteUnauthorizedAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return context.Response.WriteAsJsonAsync(new { error = "Not authenticated." });
    }

    private string? ResolveUiPath()
    {
        if (!string.IsNullOrWhiteSpace(_uiPathOverride) &&
            File.Exists(Path.Combine(_uiPathOverride, "index.html")))
        {
            return Path.GetFullPath(_uiPathOverride);
        }

        var configured = Environment.GetEnvironmentVariable("VELDRATH_DESKTOP_UI");
        if (!string.IsNullOrWhiteSpace(configured) &&
            File.Exists(Path.Combine(configured, "index.html")))
        {
            return Path.GetFullPath(configured);
        }

        var bundled = Path.Combine(AppContext.BaseDirectory, "SvelteApp");
        if (File.Exists(Path.Combine(bundled, "index.html")))
            return bundled;

        var dev = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../web/client/build-desktop"));
        if (File.Exists(Path.Combine(dev, "index.html")))
            return dev;

        return null;
    }
}
