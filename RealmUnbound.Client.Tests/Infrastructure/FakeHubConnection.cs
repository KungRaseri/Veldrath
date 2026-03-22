using RealmUnbound.Client.Services;

namespace RealmUnbound.Client.Tests.Infrastructure;

/// <summary>
/// In-memory <see cref="IHubConnection"/> for unit-testing <see cref="ServerConnectionService"/>.
/// </summary>
public class FakeHubConnection : IHubConnection
{
    // ── Behaviour controls ────────────────────────────────────────────────────
    public bool StartShouldThrow    { get; set; }
    public Exception? StartException { get; set; } = new InvalidOperationException("Simulated connect failure");

    // ── Call tracking ─────────────────────────────────────────────────────────
    public int  StartCallCount    { get; private set; }
    public int  StopCallCount     { get; private set; }
    public int  DisposeCallCount  { get; private set; }

    // ── Stored handlers ───────────────────────────────────────────────────────
    private readonly Dictionary<string, object>         _onHandlers = new();
    private          Func<Exception?, Task>?            _closedHandler;
    private          Func<string?, Task>?               _reconnectedHandler;

    // ── Events ────────────────────────────────────────────────────────────────
    public event Func<Exception?, Task>? Closed
    {
        add    => _closedHandler += value;
        remove => _closedHandler -= value;
    }

    public event Func<string?, Task>? Reconnected
    {
        add    => _reconnectedHandler += value;
        remove => _reconnectedHandler -= value;
    }

    // ── IHubConnection methods ────────────────────────────────────────────────
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        StartCallCount++;
        if (StartShouldThrow) throw StartException!;
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        StopCallCount++;
        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TResult>(string methodName)
        => Task.FromResult(default(TResult)!);

    public Task<TResult> InvokeAsync<TResult>(string methodName, object arg)
        => Task.FromResult(default(TResult)!);

    public IDisposable On<T>(string methodName, Action<T> handler)
    {
        _onHandlers[methodName] = handler;
        return new DummyDisposable();
    }

    public ValueTask DisposeAsync()
    {
        DisposeCallCount++;
        return ValueTask.CompletedTask;
    }

    // ── Test helpers ──────────────────────────────────────────────────────────

    /// <summary>Simulates the server closing the connection.</summary>
    public Task SimulateClosedAsync(Exception? error = null)
        => _closedHandler?.Invoke(error) ?? Task.CompletedTask;

    /// <summary>Simulates the client successfully reconnecting.</summary>
    public Task SimulateReconnectedAsync(string? connectionId = "new-conn-id")
        => _reconnectedHandler?.Invoke(connectionId) ?? Task.CompletedTask;

    private sealed class DummyDisposable : IDisposable { public void Dispose() { } }
}

/// <summary>
/// Factory that always returns the same pre-wired <see cref="FakeHubConnection"/>.
/// </summary>
public class FakeHubConnectionFactory : IHubConnectionFactory
{
    public FakeHubConnection Connection { get; } = new();
    public string? LastCreatedUrl { get; private set; }

    public IHubConnection CreateConnection(string hubUrl, Func<Task<string?>> accessTokenProvider)
    {
        LastCreatedUrl = hubUrl;
        return Connection;
    }
}
