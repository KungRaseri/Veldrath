using Veldrath.GameClient.Core.Models;

namespace Veldrath.GameClient.Core.Abstractions;

/// <summary>
/// Abstraction over a SignalR hub connection to the game server's <c>GameHub</c>.
/// Provides connect/disconnect lifecycle, send semantics, and handler registration.
/// Implementations manage the underlying <c>HubConnection</c> and its retry policy.
/// </summary>
public interface IGameHubConnectionService
{
    /// <summary>Gets whether the underlying hub connection is currently in the <c>Connected</c> state.</summary>
    bool IsConnected { get; }

    /// <summary>Gets the current connection state of the hub connection.</summary>
    ConnectionState State { get; }

    /// <summary>Raised when the <see cref="State"/> property changes.</summary>
    event EventHandler<ConnectionState>? StateChanged;

    /// <summary>
    /// Establishes a connection to the game server's hub.
    /// </summary>
    /// <param name="serverUrl">The base URL of the game server (e.g. <c>http://localhost:5000</c>).</param>
    /// <param name="accessToken">The JWT access token used to authenticate the SignalR connection.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ConnectAsync(string serverUrl, string accessToken, CancellationToken ct = default);

    /// <summary>
    /// Gracefully disconnects the hub connection.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task DisconnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Sends a hub method invocation with one argument.
    /// </summary>
    /// <param name="methodName">The name of the hub method to invoke.</param>
    /// <param name="arg1">The argument to pass to the hub method.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendAsync(string methodName, object? arg1, CancellationToken ct = default);

    /// <summary>
    /// Sends a hub method invocation with two arguments.
    /// </summary>
    /// <param name="methodName">The name of the hub method to invoke.</param>
    /// <param name="arg1">The first argument.</param>
    /// <param name="arg2">The second argument.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendAsync(string methodName, object? arg1, object? arg2, CancellationToken ct = default);

    /// <summary>
    /// Registers a persistent handler for a hub event that receives one argument.
    /// The handler remains active until the returned <see cref="IDisposable"/> is disposed.
    /// May be called before <see cref="ConnectAsync"/>; registrations are deferred and applied
    /// when the connection is established.
    /// </summary>
    /// <typeparam name="T">The type of the event payload.</typeparam>
    /// <param name="methodName">The name of the hub event to subscribe to.</param>
    /// <param name="handler">The asynchronous handler delegate.</param>
    /// <returns>An <see cref="IDisposable"/> that unsubscribes the handler when disposed.</returns>
    IDisposable On<T>(string methodName, Func<T, Task> handler);

    /// <summary>
    /// Registers a persistent handler for a hub event that receives two arguments.
    /// The handler remains active until the returned <see cref="IDisposable"/> is disposed.
    /// May be called before <see cref="ConnectAsync"/>; registrations are deferred and applied
    /// when the connection is established.
    /// </summary>
    /// <typeparam name="T1">The type of the first argument.</typeparam>
    /// <typeparam name="T2">The type of the second argument.</typeparam>
    /// <param name="methodName">The name of the hub event to subscribe to.</param>
    /// <param name="handler">The asynchronous handler delegate.</param>
    /// <returns>An <see cref="IDisposable"/> that unsubscribes the handler when disposed.</returns>
    IDisposable On<T1, T2>(string methodName, Func<T1, T2, Task> handler);
}
