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

    /// <summary>
    /// Gets a value indicating whether the server is reachable.
    /// Returns <see langword="true"/> when <see cref="Status"/> is <see cref="ServerStatus.Online"/>
    /// or <see cref="ServerStatus.Unknown"/> (i.e., before the first check has completed),
    /// so that the offline banner and command gates are not triggered prematurely during startup.
    /// </summary>
    bool IsOnline { get; }

    /// <summary>Gets the human-readable status message for display in the UI.</summary>
    string StatusMessage { get; }

    /// <summary>Pings the server health endpoint and updates <see cref="Status"/>.</summary>
    Task CheckAsync(string serverUrl, CancellationToken ct = default);

    /// <summary>
    /// Starts a background polling loop that calls <see cref="CheckAsync"/> at a regular cadence
    /// until <paramref name="ct"/> is cancelled. Uses a shorter interval when the server is
    /// offline so reconnection is detected quickly.
    /// </summary>
    /// <param name="getServerUrl">Delegate called each iteration to obtain the current server base URL.</param>
    /// <param name="ct">Token used to stop the loop.</param>
    Task StartPollingAsync(Func<string> getServerUrl, CancellationToken ct = default);
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

    // Configurable for testing — default values are appropriate for production use.
    internal TimeSpan OnlinePollInterval  = TimeSpan.FromSeconds(30);
    internal TimeSpan OfflinePollInterval = TimeSpan.FromSeconds(5);

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
    public bool IsOnline => Status is ServerStatus.Online or ServerStatus.Unknown;

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
        catch (OperationCanceledException)
        {
            // Cancellation is expected during shutdown — do not treat it as a server failure.
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Server health check failed for {Url}", serverUrl);
            Status = ServerStatus.Offline;
        }
    }

    /// <inheritdoc/>
    public async Task StartPollingAsync(Func<string> getServerUrl, CancellationToken ct = default)
    {
        while (true)
        {
            // Use the fast offline interval only when the server is confirmed down.
            // Unknown (pre-first-check) gets the same slow interval as Online so the
            // poll does not race the splash screen's own CheckAsync call.
            var delay = Status == ServerStatus.Offline ? OfflinePollInterval : OnlinePollInterval;
            try
            {
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (ct.IsCancellationRequested)
                return;

            try
            {
                await CheckAsync(getServerUrl(), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
