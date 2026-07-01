using Veldrath.GameClient.Core.Abstractions;
using Veldrath.GameClient.Core.Models;

namespace Veldrath.GameClient.Core.Tests.Infrastructure;

/// <summary>
/// Fake implementation of <see cref="IGameHubConnectionService"/> for unit testing.
/// Tracks connect/disconnect/send calls and allows simulating state changes.
/// </summary>
public sealed class FakeGameHubConnectionService : IGameHubConnectionService
{
    /// <summary>Whether <see cref="ConnectAsync"/> was called.</summary>
    public bool ConnectCalled { get; private set; }

    /// <summary>Whether <see cref="DisconnectAsync"/> was called.</summary>
    public bool DisconnectCalled { get; private set; }

    /// <summary>The list of (methodName, arg1) pairs sent via <see cref="SendAsync"/>.</summary>
    public List<(string MethodName, object? Arg1)> SentCommands { get; } = [];

    /// <summary>The list of (methodName, arg1, arg2) pairs sent via the two-argument overload.</summary>
    public List<(string MethodName, object? Arg1, object? Arg2)> SentCommandsTwoArg { get; } = [];

    /// <summary>Registered hub event handlers by method name.</summary>
    public Dictionary<string, Delegate> RegisteredHandlers { get; } = [];

    /// <summary>Simulates a connection state by setting <see cref="IsConnected"/>.</summary>
    public void SimulateConnected(bool connected)
    {
        IsConnected = connected;
        State = connected ? ConnectionState.Connected : ConnectionState.Disconnected;
        StateChanged?.Invoke(this, State);
    }

    /// <inheritdoc />
    public bool IsConnected { get; private set; }

    /// <inheritdoc />
    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

    /// <inheritdoc />
    public event EventHandler<ConnectionState>? StateChanged;

    /// <inheritdoc />
    public Task ConnectAsync(string serverUrl, string accessToken, CancellationToken ct = default)
    {
        ConnectCalled = true;
        SimulateConnected(true);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DisconnectAsync(CancellationToken ct = default)
    {
        DisconnectCalled = true;
        SimulateConnected(false);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendAsync(string methodName, object? arg1, CancellationToken ct = default)
    {
        SentCommands.Add((methodName, arg1));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendAsync(string methodName, object? arg1, object? arg2, CancellationToken ct = default)
    {
        SentCommandsTwoArg.Add((methodName, arg1, arg2));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IDisposable On<T>(string methodName, Func<T, Task> handler)
    {
        RegisteredHandlers[methodName] = handler;
        return new NoopDisposable();
    }

    /// <inheritdoc />
    public IDisposable On<T1, T2>(string methodName, Func<T1, T2, Task> handler)
    {
        RegisteredHandlers[methodName] = handler;
        return new NoopDisposable();
    }

    /// <summary>
    /// Simulates receiving a hub event with one argument, invoking the registered handler.
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="methodName">The hub method name.</param>
    /// <param name="arg">The payload argument.</param>
    public async Task SimulateEvent<T>(string methodName, T arg)
    {
        if (RegisteredHandlers.TryGetValue(methodName, out var handler) && handler is Func<T, Task> typed)
        {
            await typed(arg);
        }
    }

    /// <summary>No-op disposable for test subscriptions.</summary>
    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
