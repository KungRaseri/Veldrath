using Veldrath.GameClient.Core.Abstractions;
using Veldrath.GameClient.Core.Models;

namespace Veldrath.GameClient.Components.Tests.Infrastructure;

/// <summary>
/// Configurable stub for <see cref="IGameHubConnectionService"/>.
/// Default behaviour: simulates a connected hub with no-op event handlers.
/// Set <see cref="ConnectShouldThrow"/> to simulate connection failures.
/// </summary>
public sealed class FakeGameHubConnectionService : IGameHubConnectionService
{
    private ConnectionState _state = ConnectionState.Connected;
    private readonly Dictionary<string, object> _handlers = [];
    private readonly List<IDisposable> _subscriptions = [];

    /// <summary>
    /// Gets or sets the connection state reported by <see cref="IsConnected"/> and <see cref="State"/>.
    /// </summary>
    public ConnectionState StateValue
    {
        get => _state;
        set
        {
            _state = value;
            StateChanged?.Invoke(this, value);
        }
    }

    /// <inheritdoc />
    public bool IsConnected => _state == ConnectionState.Connected;

    /// <inheritdoc />
    public ConnectionState State => _state;

    /// <inheritdoc />
    public event EventHandler<ConnectionState>? StateChanged;

    /// <summary>When set to <c>true</c>, <see cref="ConnectAsync"/> throws <see cref="InvalidOperationException"/>.</summary>
    public bool ConnectShouldThrow { get; set; }

    /// <summary>When set to <c>true</c>, <see cref="SendAsync(string, object?, CancellationToken)"/> throws.</summary>
    public bool SendShouldThrow { get; set; }

    /// <summary>Records every (method, arg) pair sent via <see cref="SendAsync(string, object?, CancellationToken)"/>.</summary>
    public List<(string Method, object? Arg)> SentCommands { get; } = [];

    /// <summary>Records every (method, arg1, arg2) triple sent via <see cref="SendAsync(string, object?, object?, CancellationToken)"/>.</summary>
    public List<(string Method, object? Arg1, object? Arg2)> SentCommands2 { get; } = [];

    /// <summary>Records every method sent via <see cref="SendAsync(string, CancellationToken)"/>.</summary>
    public List<string> SentMethods { get; } = [];

    /// <inheritdoc />
    public Task ConnectAsync(string serverUrl, string accessToken, CancellationToken ct = default)
    {
        if (ConnectShouldThrow)
            throw new InvalidOperationException("Connection failed (stub).");
        StateValue = ConnectionState.Connected;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DisconnectAsync(CancellationToken ct = default)
    {
        StateValue = ConnectionState.Disconnected;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendAsync(string methodName, CancellationToken ct = default)
    {
        SentMethods.Add(methodName);
        if (SendShouldThrow)
            throw new InvalidOperationException("Send failed (stub).");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendAsync(string methodName, object? arg1, CancellationToken ct = default)
    {
        SentCommands.Add((methodName, arg1));
        if (SendShouldThrow)
            throw new InvalidOperationException("Send failed (stub).");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendAsync(string methodName, object? arg1, object? arg2, CancellationToken ct = default)
    {
        SentCommands2.Add((methodName, arg1, arg2));
        if (SendShouldThrow)
            throw new InvalidOperationException("Send failed (stub).");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IDisposable On<T>(string methodName, Func<T, Task> handler)
    {
        _handlers[methodName] = handler;
        var sub = new StubDisposable();
        _subscriptions.Add(sub);
        return sub;
    }

    /// <inheritdoc />
    public IDisposable On<T1, T2>(string methodName, Func<T1, T2, Task> handler)
    {
        _handlers[methodName] = handler;
        var sub = new StubDisposable();
        _subscriptions.Add(sub);
        return sub;
    }

    /// <summary>
    /// Simulates a hub event by invoking the handler registered for the given method.
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="methodName">The hub method name.</param>
    /// <param name="payload">The event payload.</param>
    public void FireEvent<T>(string methodName, T payload)
    {
        if (_handlers.TryGetValue(methodName, out var h))
        {
            var handler = (Func<T, Task>)h;
            handler(payload).GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Simulates a hub event with two parameters.
    /// </summary>
    public void FireEvent<T1, T2>(string methodName, T1 arg1, T2 arg2)
    {
        if (_handlers.TryGetValue(methodName, out var h))
        {
            var handler = (Func<T1, T2, Task>)h;
            handler(arg1, arg2).GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Disposes all active subscriptions.
    /// </summary>
    public void DisposeSubscriptions()
    {
        foreach (var sub in _subscriptions)
            sub.Dispose();
        _subscriptions.Clear();
    }

    /// <summary>
    /// A disposable that does nothing on dispose (for stub lifecycle management).
    /// </summary>
    private sealed class StubDisposable : IDisposable
    {
        /// <inheritdoc />
        public void Dispose() { }
    }
}
