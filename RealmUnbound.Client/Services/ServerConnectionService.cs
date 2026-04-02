using Microsoft.Extensions.Logging;

namespace RealmUnbound.Client.Services;

public enum ConnectionState { Disconnected, Connecting, Connected, Failed }

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
}

public class ServerConnectionService : IServerConnectionService, IAsyncDisposable
{
    private readonly ILogger<ServerConnectionService> _logger;
    private readonly TokenStore _tokens;
    private readonly IHubConnectionFactory _connectionFactory;
    private readonly IAuthService _auth;
    private IHubConnection? _connection;
    private ConnectionState _state = ConnectionState.Disconnected;

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
            State = ConnectionState.Disconnected;
            _logger.LogWarning(error, "Connection closed");
            ConnectionLost?.Invoke();
            await Task.CompletedTask;
        };

        _connection.Reconnected += (connectionId) =>
        {
            State = ConnectionState.Connected;
            _logger.LogInformation("Reconnected: {ConnectionId}", connectionId);
            return Task.CompletedTask;
        };

        try
        {
            await _connection.StartAsync(cancellationToken);
            State = ConnectionState.Connected;
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

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
