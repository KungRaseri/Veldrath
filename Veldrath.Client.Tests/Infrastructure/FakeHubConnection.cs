using Veldrath.Client.Services;

namespace Veldrath.Client.Tests.Infrastructure;

/// <summary>
/// In-memory <see cref="IHubConnection"/> for unit-testing <see cref="ServerConnectionService"/>.
/// </summary>
public class FakeHubConnection : IHubConnection
{
    // Behaviour controls
    public bool StartShouldThrow    { get; set; }
    public Exception? StartException { get; set; } = new InvalidOperationException("Simulated connect failure");

    // Call tracking
    public int  StartCallCount    { get; private set; }
    public int  StopCallCount     { get; private set; }
    public int  DisposeCallCount  { get; private set; }

    // Stored handlers
    private readonly Dictionary<string, object>         _onHandlers = new();
    private          Func<Exception?, Task>?            _closedHandler;
    private          Func<Exception?, Task>?            _reconnectingHandler;
    private          Func<string?, Task>?               _reconnectedHandler;

    // Events
    public event Func<Exception?, Task>? Closed
    {
        add    => _closedHandler += value;
        remove => _closedHandler -= value;
    }

    public event Func<Exception?, Task>? Reconnecting
    {
        add    => _reconnectingHandler += value;
        remove => _reconnectingHandler -= value;
    }

    public event Func<string?, Task>? Reconnected
    {
        add    => _reconnectedHandler += value;
        remove => _reconnectedHandler -= value;
    }

    // IHubConnection methods
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

    public IDisposable On(string methodName, Action handler)
    {
        _onHandlers[methodName] = handler;
        return new DummyDisposable();
    }

    public ValueTask DisposeAsync()
    {
        DisposeCallCount++;
        return ValueTask.CompletedTask;
    }

    // Test helpers
    /// <summary>Simulates the server closing the connection.</summary>
    public Task SimulateClosedAsync(Exception? error = null)
        => _closedHandler?.Invoke(error) ?? Task.CompletedTask;

    /// <summary>Simulates an automatic reconnect attempt in progress.</summary>
    public Task SimulateReconnectingAsync(Exception? error = null)
        => _reconnectingHandler?.Invoke(error) ?? Task.CompletedTask;

    /// <summary>Simulates the client successfully reconnecting.</summary>
    public Task SimulateReconnectedAsync(string? connectionId = "new-conn-id")
        => _reconnectedHandler?.Invoke(connectionId) ?? Task.CompletedTask;

    /// <summary>Simulates a server-to-client message with a typed payload.</summary>
    public void SimulateReceive<T>(string methodName, T payload)
    {
        if (_onHandlers.TryGetValue(methodName, out var h))
            ((Action<T>)h)(payload);
    }

    private sealed class DummyDisposable : IDisposable { public void Dispose() { } }
}

/// <summary>
/// Factory that returns the pre-wired <see cref="FakeHubConnection"/> on the first call
/// and fresh instances for subsequent calls, enabling retry tests.
/// </summary>
public class FakeHubConnectionFactory : IHubConnectionFactory
{
    private readonly FakeHubConnection _firstConnection = new();
    private readonly List<FakeHubConnection> _allCreated = [];
    private bool _firstCallMade;

    /// <summary>Returns the first connection created (or the pre-seeded instance if none yet).</summary>
    public FakeHubConnection Connection => _allCreated.Count > 0 ? _allCreated[0] : _firstConnection;

    /// <summary>All connections created by this factory, in creation order.</summary>
    public IReadOnlyList<FakeHubConnection> AllCreated => _allCreated;

    /// <summary>The hub URL passed to the last <see cref="CreateConnection"/> call.</summary>
    public string? LastCreatedUrl { get; private set; }

    /// <summary>The access-token provider delegate passed to the last <see cref="CreateConnection"/> call.</summary>
    public Func<Task<string?>>? LastAccessTokenProvider { get; private set; }

    /// <inheritdoc/>
    public IHubConnection CreateConnection(string hubUrl, Func<Task<string?>> accessTokenProvider)
    {
        LastCreatedUrl = hubUrl;
        LastAccessTokenProvider = accessTokenProvider;
        if (!_firstCallMade)
        {
            _firstCallMade = true;
            _allCreated.Add(_firstConnection);
            return _firstConnection;
        }
        var conn = new FakeHubConnection();
        _allCreated.Add(conn);
        return conn;
    }
}
