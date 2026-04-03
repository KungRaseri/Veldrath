using Microsoft.Extensions.Logging;

namespace RealmUnbound.Client.Services;

/// <summary>Connection health state for the game server hub.</summary>
public enum ConnectionState
{
    /// <summary>Not connected to the server.</summary>
    Disconnected,
    /// <summary>Establishing the initial connection.</summary>
    Connecting,
    /// <summary>Connected with acceptable latency.</summary>
    Connected,
    /// <summary>Connected but with elevated latency (ping ≥ 200 ms).</summary>
    Degraded,
    /// <summary>Lost the connection and SignalR is attempting to reconnect automatically.</summary>
    Reconnecting,
    /// <summary>Initial connection attempt failed with a hard error.</summary>
    Failed,
}

public interface IServerConnectionService
{
    ConnectionState State { get; }
    event Action<ConnectionState>? StateChanged;
    /// <summary>Raised when the hub connection is closed — including after a failed token refresh.</summary>
    event Action? ConnectionLost;
    Task ConnectAsync(string serverUrl, CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    Task SendCommandAsync(string method);
    Task<TResult?> SendCommandAsync<TResult>(string method);
    Task<TResult?> SendCommandAsync<TResult>(string method, object command);
    IDisposable On<T>(string method, Action<T> handler);
    /// <summary>Registers a handler for a server-to-client message that carries no payload.</summary>
    IDisposable On(string method, Action handler);
    /// <summary>Measures round-trip latency to the server in milliseconds, or <see langword="null"/> when not connected.</summary>
    Task<long?> MeasurePingAsync();
}

public class ServerConnectionService : IServerConnectionService, IAsyncDisposable
{
    private readonly ILogger<ServerConnectionService> _logger;
    private readonly TokenStore _tokens;
    private readonly IHubConnectionFactory _connectionFactory;
    private readonly IAuthService _auth;
    private IHubConnection? _connection;
    private ConnectionState _state = ConnectionState.Disconnected;
    private System.Timers.Timer? _pingTimer;

    public ConnectionState State
    {
        get => _state;
        private set
        {
            _state = value;
            StateChanged?.Invoke(value);
        }
    }

    public event Action<ConnectionState>? StateChanged;

    /// <inheritdoc />
    public event Action? ConnectionLost;

    /// <summary>Initializes a new instance of <see cref="ServerConnectionService"/>.</summary>
    public ServerConnectionService(
        ILogger<ServerConnectionService> logger,
        TokenStore tokens,
        IHubConnectionFactory connectionFactory,
        IAuthService auth)
    {
        _logger = logger;
        _tokens = tokens;
        _connectionFactory = connectionFactory;
        _auth = auth;
    }

    public async Task ConnectAsync(string serverUrl, CancellationToken cancellationToken = default)
    {
        if (State == ConnectionState.Connected) return;

        State = ConnectionState.Connecting;
        _logger.LogInformation("Connecting to server: {Url}", serverUrl);

        _connection = _connectionFactory.CreateConnection(
            $"{serverUrl}/hubs/game",
            async () =>
            {
                // Silently refresh the access token on every (re)connection attempt so that
                // automatic reconnects after a network blip don't fail with 401 due to an
                // expired token. The RefreshToken remains valid for 30 days.
                if (_tokens.IsExpiringSoon && _tokens.RefreshToken is not null)
                {
                    var refreshed = await _auth.RefreshAsync();
                    if (!refreshed)
                    {
                        _logger.LogWarning("Token refresh failed during hub connect — forcing disconnect");
                        return null;
                    }
                }
                return _tokens.AccessToken;
            });

        _connection.Closed += async (error) =>
        {
            StopPingTimer();
            State = ConnectionState.Disconnected;
            _logger.LogWarning(error, "Connection closed");
            ConnectionLost?.Invoke();
            await Task.CompletedTask;
        };

        _connection.Reconnecting += (error) =>
        {
            StopPingTimer();
            State = ConnectionState.Reconnecting;
            _logger.LogWarning(error, "Connection reconnecting");
            return Task.CompletedTask;
        };

        _connection.Reconnected += (connectionId) =>
        {
            State = ConnectionState.Connected;
            StartPingTimer();
            _logger.LogInformation("Reconnected: {ConnectionId}", connectionId);
            return Task.CompletedTask;
        };

        try
        {
            await _connection.StartAsync(cancellationToken);
            State = ConnectionState.Connected;
            StartPingTimer();
            _logger.LogInformation("Connected to server");
        }
        catch (Exception ex)
        {
            State = ConnectionState.Failed;
            _logger.LogError(ex, "Failed to connect to server");
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_connection is not null)
        {
            StopPingTimer();
            await _connection.StopAsync();
            State = ConnectionState.Disconnected;
        }
    }

    public async Task SendCommandAsync(string method)
    {
        if (_connection is null || State != ConnectionState.Connected)
            throw new InvalidOperationException("Not connected to server.");

        await _connection.InvokeAsync<object?>(method);
    }

    public async Task<TResult?> SendCommandAsync<TResult>(string method)
    {
        if (_connection is null || State != ConnectionState.Connected)
            throw new InvalidOperationException("Not connected to server.");

        return await _connection.InvokeAsync<TResult>(method);
    }

    public async Task<TResult?> SendCommandAsync<TResult>(string method, object command)
    {
        if (_connection is null || State != ConnectionState.Connected)
            throw new InvalidOperationException("Not connected to server.");

        return await _connection.InvokeAsync<TResult>(method, command);
    }

    public IDisposable On<T>(string method, Action<T> handler)
    {
        if (_connection is null)
            throw new InvalidOperationException("Not connected to server.");
        return _connection.On(method, handler);
    }

    /// <inheritdoc/>
    public IDisposable On(string method, Action handler)
    {
        if (_connection is null)
            throw new InvalidOperationException("Not connected to server.");
        return _connection.On(method, handler);
    }

    /// <inheritdoc/>
    public async Task<long?> MeasurePingAsync()
    {
        if (_connection is null || (_state != ConnectionState.Connected && _state != ConnectionState.Degraded))
            return null;
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await _connection.InvokeAsync<long>("Ping");
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ping failed; skipping cycle");
            return null;
        }
    }

    private void StartPingTimer()
    {
        StopPingTimer();
        _pingTimer = new System.Timers.Timer(5_000) { AutoReset = true };
        _pingTimer.Elapsed += async (_, _) =>
        {
            var rtt = await MeasurePingAsync();
            if (rtt is null) return;
            State = rtt < 200 ? ConnectionState.Connected : ConnectionState.Degraded;
        };
        _pingTimer.Start();
    }

    private void StopPingTimer()
    {
        _pingTimer?.Stop();
        _pingTimer?.Dispose();
        _pingTimer = null;
    }

    public async ValueTask DisposeAsync()
    {
        StopPingTimer();
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
