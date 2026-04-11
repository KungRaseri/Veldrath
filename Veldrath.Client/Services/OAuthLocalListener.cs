using System.Net;
using System.Web;

namespace Veldrath.Client.Services;

/// <summary>
/// Spins up a temporary localhost HTTP listener that catches the JWT redirect
/// from the Veldrath.Server after a successful OAuth provider exchange.
/// One-shot: use, await WaitForCallbackAsync, then dispose.
/// </summary>
internal sealed class OAuthLocalListener : IDisposable
{
    private readonly HttpListener _listener;

    /// <summary>The full callback URL to pass as the <c>returnUrl</c> query parameter.</summary>
    public string CallbackUrl { get; }

    public OAuthLocalListener()
    {
        // Pick an available port by binding to port 0 and reading back the assigned port.
        var port = GetFreePort();
        CallbackUrl = $"http://localhost:{port}/callback";
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Start();
    }

    /// <summary>
    /// Waits for the browser to hit the callback URL and parses the single-use exchange code.
    /// Returns <c>null</c> on timeout, cancellation, or a malformed callback.
    /// </summary>
    public async Task<OAuthCallbackResult?> WaitForCallbackAsync(CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMinutes(5));

        try
        {
            var contextTask = _listener.GetContextAsync();
            await Task.WhenAny(contextTask, Task.Delay(Timeout.Infinite, cts.Token));

            if (!contextTask.IsCompleted) return null;

            var ctx = await contextTask;
            var query = HttpUtility.ParseQueryString(ctx.Request.Url?.Query ?? "");

            // Acknowledge the browser — a minimal HTML page so the user can close the tab.
            var html =
                """
                <html><head><title>Veldrath — Authenticated</title></head>
                <body style="font-family:system-ui;text-align:center;padding-top:3rem">
                <h2>Authentication successful!</h2>
                <p>You can close this tab and return to the game.</p>
                </body></html>
                """;
            var bytes = System.Text.Encoding.UTF8.GetBytes(html);
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.ContentLength64 = bytes.Length;
            await ctx.Response.OutputStream.WriteAsync(bytes, ct);
            ctx.Response.Close();

            // Server redirects with ?code=<exchange-code>&aid=<account-id>
            var code = query["code"];
            var aid  = query["aid"];

            if (string.IsNullOrEmpty(code) || !Guid.TryParse(aid, out var accountId))
                return null;

            return new OAuthCallbackResult(code, accountId);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        try { _listener.Stop(); }  catch { /* already stopped */ }
        try { _listener.Close(); } catch { /* already closed */ }
    }

    // Bind to OS-assigned port 0, read it back, then release.
    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

/// <summary>Carries the single-use exchange code returned by the server OAuth callback.</summary>
internal sealed record OAuthCallbackResult(
    /// <summary>64-character hex exchange code; valid for 60 seconds.</summary>
    string Code,
    /// <summary>Account ID the code was issued for.</summary>
    Guid AccountId);
