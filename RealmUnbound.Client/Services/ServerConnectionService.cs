using System.Reflection;
using Microsoft.Extensions.Logging;
using Veldrath.Contracts.Connection;

namespace Veldrath.Client.Services;

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
    /// <summary>
    /// Raised when the server reports a client version that is incompatible, or when the
    /// server version is older than this client requires. The connection is already
    /// disconnected when this event fires.
    /// Parameters: <c>(clientVersion, serverVersion)</c>.
    /// </summary>
    event Action<string, string>? VersionMismatch;
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

    /// <inheritdoc />
    public event Action<string, string>? VersionMismatch;

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

        // Subscribe persistently to ServerInfo so version compatibility is checked on every
        // connect and reconnect (catches mid-session server updates too).
        _connection.On<ServerInfoPayload>("ServerInfo", HandleServerInfo);

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

    private void HandleServerInfo(ServerInfoPayload payload)
    {
        var v = typeof(ServerConnectionService).Assembly.GetName().Version ?? new Version(0, 1);
        var clientVersion = $"{v.Major}.{v.Minor}";

        // Server rejects this client (authoritative check — enforced server-side too)
        if (!IsVersionCompatible(clientVersion, payload.MinCompatibleClientVersion))
        {
            _logger.LogWarning(
                "Version mismatch: client v{Client} is below server minimum v{Min}",
                clientVersion, payload.MinCompatibleClientVersion);
            VersionMismatch?.Invoke(clientVersion, payload.ServerVersion);
            _ = DisconnectAsync();
            return;
        }

        // Client rejects this server (UX gate — client requires a feature the server lacks)
        var minServerAttr = typeof(ServerConnectionService).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "MinCompatibleServerVersion");
        var minServerVersion = minServerAttr?.Value ?? "0.1";

        if (!IsVersionCompatible(payload.ServerVersion, minServerVersion))
        {
            _logger.LogWarning(
                "Version mismatch: server v{Server} is below client minimum v{Min}",
                payload.ServerVersion, minServerVersion);
            VersionMismatch?.Invoke(clientVersion, payload.ServerVersion);
            _ = DisconnectAsync();
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="version"/> meets or exceeds
    /// <paramref name="minVersion"/>. Requires the same major; minor must be ≥ min minor.
    /// Patch numbers carry no compatibility meaning and are ignored.
    /// </summary>
    private static bool IsVersionCompatible(string version, string minVersion)
    {
        if (!TryParseVersion(version,    out var maj,    out var min))    return false;
        if (!TryParseVersion(minVersion, out var minMaj, out var minMin)) return false;
        return maj == minMaj && min >= minMin;
    }

    private static bool TryParseVersion(string v, out int major, out int minor)
    {
        major = 0;
        minor = 0;
        var parts = v.Split('.');
        if (parts.Length < 2) return false;
        return int.TryParse(parts[0], out major) && int.TryParse(parts[1], out minor);
    }

    public async ValueTask DisposeAsync()
    {
        StopPingTimer();
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
