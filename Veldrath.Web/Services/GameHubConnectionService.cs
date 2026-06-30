using Microsoft.AspNetCore.SignalR.Client;

namespace Veldrath.Web.Services;

/// <summary>
/// Manages the server-to-server SignalR <see cref="HubConnection"/> to Veldrath.Server's
/// GameHub at <c>/hubs/game</c>.  Registered as a scoped service so each Blazor circuit
/// gets its own isolated game connection.
/// </summary>
public sealed class GameHubConnectionService : IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GameHubConnectionService> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Holds handler registrations that were made before <see cref="ConnectAsync"/> was called.
    /// Applied to _connection after it is built, before it is started.
    /// </summary>
    private readonly List<Func<HubConnection, IDisposable>> _pendingOnRegistrations = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="GameHubConnectionService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving scoped dependencies.</param>
    /// <param name="logger">The logger instance.</param>
    public GameHubConnectionService(
        IServiceProvider serviceProvider,
        ILogger<GameHubConnectionService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Returns <c>true</c> when the underlying <see cref="HubConnection"/> is in the
    /// <see cref="HubConnectionState.Connected"/> state.
    /// </summary>
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    /// <summary>
    /// Establishes the <see cref="HubConnection"/> to the game server's GameHub.
    /// </summary>
    /// <param name="serverUrl">The base URL of the game server (e.g. <c>http://localhost:5000</c>).</param>
    /// <param name="accessToken">The JWT access token used to authenticate the SignalR connection.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the service has been disposed.</exception>
    public async Task ConnectAsync(string serverUrl, string accessToken, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _connectionLock.WaitAsync(ct);
        try
        {
            if (_connection?.State == HubConnectionState.Connected)
            {
                _logger.LogWarning("GameHub connection is already connected.");
                return;
            }

            // Tear down any previous connection before building a new one.
            if (_connection is not null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }

            var hubUrl = $"{serverUrl.TrimEnd('/')}/hubs/game";

            _connection = new HubConnectionBuilder()
                .WithUrl(hubUrl, opts =>
                {
                    opts.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                })
                .WithAutomaticReconnect(new RetryPolicy())
                .Build();

            // Apply any handler registrations that were made before ConnectAsync was called.
            foreach (var registration in _pendingOnRegistrations)
            {
                registration(_connection);
            }
            _pendingOnRegistrations.Clear();

            _connection.Closed += reason =>
            {
                _logger.LogWarning(reason, "GameHub connection closed.");
                return Task.CompletedTask;
            };

            await _connection.StartAsync(ct);
            _logger.LogInformation("GameHub connection established to {HubUrl}.", hubUrl);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Disconnects the <see cref="HubConnection"/> gracefully.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await _connectionLock.WaitAsync(ct);
        try
        {
            if (_connection is not null)
            {
                await _connection.StopAsync(ct);
                _logger.LogInformation("GameHub connection stopped.");
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Sends a hub method invocation (calls a GameHub method on the server) with one argument.
    /// </summary>
    /// <param name="methodName">The name of the hub method to invoke.</param>
    /// <param name="arg1">The argument to pass to the hub method.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the connection is not established.</exception>
    public async Task SendAsync(string methodName, object? arg1, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_connection is null || _connection.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException("GameHub connection is not established.");
        }

        await _connection.InvokeAsync(methodName, arg1, ct);
    }

    /// <summary>
    /// Sends a hub method invocation with no arguments.
    /// </summary>
    /// <param name="methodName">The name of the hub method to invoke.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the connection is not established.</exception>
    public async Task SendAsync(string methodName, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_connection is null || _connection.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException("GameHub connection is not established.");
        }

        await _connection.InvokeAsync(methodName, ct);
    }

    /// <summary>
    /// Registers a handler for a hub event that receives one argument.
    /// Must be called before <see cref="ConnectAsync"/> to ensure the handler is attached
    /// before the connection starts receiving messages.
    /// </summary>
    /// <typeparam name="T1">The type of the first argument.</typeparam>
    /// <param name="methodName">The name of the hub event to subscribe to.</param>
    /// <param name="handler">The handler delegate to invoke when the event fires.</param>
    /// <returns>An <see cref="IDisposable"/> that unsubscribes the handler when disposed.</returns>
    public IDisposable On<T1>(string methodName, Func<T1, Task> handler)
    {
        if (_connection is not null)
        {
            return _connection.On(methodName, handler);
        }

        // Connection not yet built — defer the handler registration so it is applied
        // in ConnectAsync after the HubConnection is created but before it is started.
        var deferred = new DeferredDisposable();
        _pendingOnRegistrations.Add(conn =>
        {
            var real = conn.On(methodName, handler);
            deferred.SetInner(real);
            return real;
        });

        _logger.LogDebug(
            "On<{T1}>(\"{Method}\") deferred — _connection was null; will register during ConnectAsync.",
            typeof(T1).Name, methodName);

        return deferred;
    }

    /// <summary>
    /// Registers a handler for a hub event with no payload.
    /// Must be called before <see cref="ConnectAsync"/> to ensure the handler is attached
    /// before the connection starts receiving messages.
    /// </summary>
    /// <param name="methodName">The name of the hub event to subscribe to.</param>
    /// <param name="handler">The handler delegate to invoke when the event fires.</param>
    /// <returns>An <see cref="IDisposable"/> that unsubscribes the handler when disposed.</returns>
    public IDisposable On(string methodName, Func<Task> handler)
    {
        if (_connection is not null)
        {
            return _connection.On(methodName, handler);
        }

        // Connection not yet built — defer the handler registration so it is applied
        // in ConnectAsync after the HubConnection is created but before it is started.
        var deferred = new DeferredDisposable();
        _pendingOnRegistrations.Add(conn =>
        {
            var real = conn.On(methodName, handler);
            deferred.SetInner(real);
            return real;
        });

        _logger.LogDebug(
            "On(\"{Method}\") deferred — _connection was null; will register during ConnectAsync.",
            methodName);

        return deferred;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_connection is not null)
        {
            try
            {
                await _connection.StopAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while stopping GameHub connection during dispose.");
            }

            await _connection.DisposeAsync();
        }

        _connectionLock.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Simple retry policy for automatic SignalR reconnection.
    /// Retries at 0s, 2s, 10s, 30s then gives up.
    /// </summary>
    private sealed class RetryPolicy : IRetryPolicy
    {
        private readonly TimeSpan[] _retryDelays =
        [
            TimeSpan.Zero,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(30)
        ];

        private int _attempt;

        /// <inheritdoc />
        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            if (_attempt < _retryDelays.Length)
            {
                return _retryDelays[_attempt++];
            }

            return null; // Give up after exhausting all retry attempts.
        }
    }

    /// <summary>
    /// An <see cref="IDisposable"/> that wraps a real disposable that may not exist yet at
    /// construction time.  Used to return an <see cref="IDisposable"/> from <see cref="On{T1}"/>
    /// and <see cref="On(string, Func{Task})"/> when handlers are registered before
    /// <see cref="ConnectAsync"/> has built the underlying <see cref="HubConnection"/>.
    /// Once the real disposable is set, forwards <see cref="Dispose"/> to it.
    /// </summary>
    private sealed class DeferredDisposable : IDisposable
    {
        private IDisposable? _inner;
        private bool _disposed;

        /// <summary>Sets the real <see cref="IDisposable"/> that this wrapper delegates to.</summary>
        /// <param name="inner">The actual subscription disposable from <c>HubConnection.On</c>.</param>
        public void SetInner(IDisposable inner)
        {
            _inner = inner;
            if (_disposed)
            {
                inner.Dispose();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _inner?.Dispose();
        }
    }
}
