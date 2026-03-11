using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.SignalR.Client;

namespace RealmUnbound.Client.Services;

/// <summary>
/// Abstracts the subset of <see cref="HubConnection"/> used by
/// <see cref="ServerConnectionService"/> so the service can be unit-tested
/// without a real WebSocket transport.
/// </summary>
public interface IHubConnection : IAsyncDisposable
{
    event Func<Exception?, Task>? Closed;
    event Func<string?, Task>? Reconnected;
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
    Task<TResult> InvokeAsync<TResult>(string methodName, object arg);
    IDisposable On<T>(string methodName, Action<T> handler);
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

    public event Func<string?, Task>? Reconnected
    {
        add    => _inner.Reconnected += value;
        remove => _inner.Reconnected -= value;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
        => _inner.StartAsync(cancellationToken);

    public Task StopAsync() => _inner.StopAsync();

    public Task<TResult> InvokeAsync<TResult>(string methodName, object arg)
        => _inner.InvokeAsync<TResult>(methodName, arg);

    public IDisposable On<T>(string methodName, Action<T> handler)
        => _inner.On(methodName, handler);

    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
