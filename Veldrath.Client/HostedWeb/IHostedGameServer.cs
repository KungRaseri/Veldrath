namespace Veldrath.Client.HostedWeb;

/// <summary>
/// Manages the lifecycle of an embedded ASP.NET Core web server that hosts the
/// <c>Veldrath.GameClient.Components</c> Razor Class Library for rendering inside an
/// Avalonia WebView2 control.  The server binds to <c>127.0.0.1</c> with a random
/// port so it is only accessible from the local machine.
/// </summary>
public interface IHostedGameServer
{
    /// <summary>The randomly assigned TCP port the server is listening on, or <c>0</c> before startup.</summary>
    int Port { get; }

    /// <summary>Whether the embedded web server is currently running.</summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets the base URL of the embedded server (e.g. <c>http://localhost:54321</c>).
    /// Returns <c>null</c> before the server has started.
    /// </summary>
    string? BaseUrl { get; }

    /// <summary>Starts the embedded web server asynchronously.</summary>
    /// <param name="ct">Cancellation token that will trigger a graceful shutdown.</param>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>Stops the embedded web server gracefully.</summary>
    /// <param name="ct">Cancellation token for the shutdown operation.</param>
    Task StopAsync(CancellationToken ct = default);
}
