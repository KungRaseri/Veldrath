using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Veldrath.GameClient.Core.Abstractions;
using Veldrath.GameClient.Core.Models;

namespace Veldrath.GameClient.Core.Services;

/// <summary>
/// Manages the server-to-server SignalR <see cref="HubConnection"/> to Veldrath.Server's
/// GameHub at <c>/hubs/game</c>.  Registered as a scoped service so each Blazor circuit
/// gets its own isolated game connection.
/// Implements <see cref="IGameHubConnectionService"/> for abstraction across consumers.
/// </summary>
public sealed class GameHubConnectionService : IGameHubConnectionService, IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GameHubConnectionService> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private bool _disposed;
    private ConnectionState _state = ConnectionState.Disconnected;

    /// <summary>
    /// Holds handler registrations that were made before <see cref="ConnectAsync"/> was called.
    /// Applied to <c>_connection</c> after it is built, before it is started.
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

    /// <inheritdoc />
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    /// <inheritdoc />
    public ConnectionState State
    {
        get => _state;
        private set
        {
            if (_state == value) return;
            _state = value;
            StateChanged?.Invoke(this, value);
        }
    }

    /// <inheritdoc />
    public event EventHandler<ConnectionState>? StateChanged;

    /// <inheritdoc />
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

            State = ConnectionState.Connecting;

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
                State = ConnectionState.Disconnected;
                _logger.LogWarning(reason, "GameHub connection closed.");
                return Task.CompletedTask;
            };

            _connection.Reconnecting += _ =>
            {
                State = ConnectionState.Reconnecting;
                _logger.LogInformation("GameHub connection reconnecting...");
                return Task.CompletedTask;
            };

            _connection.Reconnected += _ =>
            {
                State = ConnectionState.Connected;
                _logger.LogInformation("GameHub connection re-established.");
                return Task.CompletedTask;
            };

            await _connection.StartAsync(ct);
            State = ConnectionState.Connected;
            _logger.LogInformation("GameHub connection established to {HubUrl}.", hubUrl);
        }
        catch
        {
            State = ConnectionState.Failed;
            throw;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <inheritdoc />
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
            State = ConnectionState.Disconnected;
            _connectionLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SendAsync(string methodName, object? arg1, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_connection is null || _connection.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException("GameHub connection is not established.");
        }

        await _connection.InvokeAsync(methodName, arg1, ct);
    }

    /// <inheritdoc />
    public async Task SendAsync(string methodName, object? arg1, object? arg2, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_connection is null || _connection.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException("GameHub connection is not established.");
        }

        await _connection.InvokeAsync(methodName, arg1, arg2, ct);
    }

    /// <summary>
    /// Sends a hub method invocation with no arguments.
    /// </summary>
    /// <param name="methodName">The name of the hub method to invoke.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task SendAsync(string methodName, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_connection is null || _connection.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException("GameHub connection is not established.");
        }

        await _connection.InvokeAsync(methodName, ct);
    }

    /// <inheritdoc />
    public IDisposable On<T>(string methodName, Func<T, Task> handler)
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
            typeof(T).Name, methodName);

        return deferred;
    }

    /// <inheritdoc />
    public IDisposable On<T1, T2>(string methodName, Func<T1, T2, Task> handler)
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
            "On<{T1},{T2}>(\"{Method}\") deferred — _connection was null; will register during ConnectAsync.",
            typeof(T1).Name, typeof(T2).Name, methodName);

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
        State = ConnectionState.Disconnected;

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
    /// construction time.  Used to return an <see cref="IDisposable"/> from <see cref="On{T}"/>
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
