using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.SignalR.Client;

namespace Veldrath.Client.Services;

/// <summary>
/// Abstracts the subset of <see cref="HubConnection"/> used by
/// <see cref="ServerConnectionService"/> so the service can be unit-tested
/// without a real WebSocket transport.
/// </summary>
public interface IHubConnection : IAsyncDisposable
{
    event Func<Exception?, Task>? Closed;
    event Func<Exception?, Task>? Reconnecting;
    event Func<string?, Task>? Reconnected;
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
    /// <summary>Invokes a hub method with no arguments.</summary>
    Task<TResult> InvokeAsync<TResult>(string methodName);
    Task<TResult> InvokeAsync<TResult>(string methodName, object arg);
    /// <summary>Registers a handler for a server-to-client message that carries a typed payload.</summary>
    IDisposable On<T>(string methodName, Action<T> handler);
    /// <summary>Registers a handler for a server-to-client message that carries no payload.</summary>
    IDisposable On(string methodName, Action handler);
}

/// <summary>
/// Wraps a real <see cref="HubConnection"/> as <see cref="IHubConnection"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class HubConnectionWrapper : IHubConnection
{
    private readonly HubConnection _inner;

    public HubConnectionWrapper(HubConnection inner) => _inner = inner;

    public event Func<Exception?, Task>? Closed
    {
        add    => _inner.Closed += value;
        remove => _inner.Closed -= value;
    }

    public event Func<Exception?, Task>? Reconnecting
    {
        add    => _inner.Reconnecting += value;
        remove => _inner.Reconnecting -= value;
    }

    public event Func<string?, Task>? Reconnected
    {
        add    => _inner.Reconnected += value;
        remove => _inner.Reconnected -= value;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
        => _inner.StartAsync(cancellationToken);

    public Task StopAsync() => _inner.StopAsync();

    public Task<TResult> InvokeAsync<TResult>(string methodName)
        => _inner.InvokeAsync<TResult>(methodName);

    public Task<TResult> InvokeAsync<TResult>(string methodName, object arg)
        => _inner.InvokeAsync<TResult>(methodName, arg);

    public IDisposable On<T>(string methodName, Action<T> handler)
        => _inner.On(methodName, handler);

    public IDisposable On(string methodName, Action handler)
        => _inner.On(methodName, handler);

    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
