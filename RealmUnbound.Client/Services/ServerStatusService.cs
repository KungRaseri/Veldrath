using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace RealmUnbound.Client.Services;

/// <summary>Server reachability states.</summary>
public enum ServerStatus
{
    /// <summary>Status has not been checked yet.</summary>
    Unknown,
    /// <summary>The server responded successfully to the health check.</summary>
    Online,
    /// <summary>The server did not respond or returned a non-success status.</summary>
    Offline
}

/// <summary>
/// Contract for the singleton service that tracks whether the game server is reachable.
/// Implements <see cref="System.ComponentModel.INotifyPropertyChanged"/> so ViewModels
/// can subscribe reactively via <c>WhenAnyValue</c>.
/// </summary>
public interface IServerStatusService : System.ComponentModel.INotifyPropertyChanged
{
    /// <summary>Gets the current server reachability status.</summary>
    ServerStatus Status { get; }

    /// <summary>Gets a value indicating whether the server is reachable.</summary>
    bool IsOnline { get; }

    /// <summary>Gets the human-readable status message for display in the UI.</summary>
    string StatusMessage { get; }

    /// <summary>Pings the server health endpoint and updates <see cref="Status"/>.</summary>
    Task CheckAsync(string serverUrl, CancellationToken ct = default);
}

/// <summary>
/// Singleton reactive service that tracks whether the game server is reachable.
/// Exposes a reactive <see cref="Status"/> property that drives the connection-status
/// banner and disables server-dependent UI actions when offline.
/// </summary>
public class ServerStatusService(IHttpClientFactory httpClientFactory, ILogger<ServerStatusService> logger)
    : ReactiveObject, IServerStatusService
{
    private ServerStatus _status = ServerStatus.Unknown;

    /// <inheritdoc/>
    public ServerStatus Status
    {
        get => _status;
        private set
        {
            this.RaiseAndSetIfChanged(ref _status, value);
            this.RaisePropertyChanged(nameof(IsOnline));
            this.RaisePropertyChanged(nameof(StatusMessage));
        }
    }

    /// <inheritdoc/>
    public bool IsOnline => Status == ServerStatus.Online;

    /// <inheritdoc/>
    public string StatusMessage => Status switch
    {
        ServerStatus.Online  => string.Empty,
        ServerStatus.Offline => "Unable to connect to server — game features are unavailable",
        _                    => "Checking server connection...",
    };

    /// <inheritdoc/>
    public async Task CheckAsync(string serverUrl, CancellationToken ct = default)
    {
        try
        {
            using var client = httpClientFactory.CreateClient();
            var response = await client.GetAsync($"{serverUrl.TrimEnd('/')}/health", ct);
            Status = response.IsSuccessStatusCode ? ServerStatus.Online : ServerStatus.Offline;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Server health check failed for {Url}", serverUrl);
            Status = ServerStatus.Offline;
        }
    }
}
