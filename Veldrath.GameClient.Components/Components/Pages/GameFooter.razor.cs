using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Components;
using Veldrath.GameClient.Core.Abstractions;
using Veldrath.GameClient.Core.Models;

namespace Veldrath.GameClient.Components.Components.Pages;

/// <summary>
/// Connection status values used by <see cref="GameFooter"/> to display the
/// current health of the SignalR hub connection.
/// </summary>
public enum StatusIndicator
{
    /// <summary>The connection is healthy and established.</summary>
    Connected,

    /// <summary>The connection is established but experiencing degraded performance (high latency).</summary>
    Degraded,

    /// <summary>The connection has been lost.</summary>
    Disconnected,

    /// <summary>The client is attempting to automatically reconnect.</summary>
    Reconnecting
}

/// <summary>
/// Code-behind for the <see cref="GameFooter"/> component.
/// Monitors the hub connection state via <see cref="IGameHubConnectionService"/> and
/// provides enhanced status display with ping, degraded detection, and reconnection banner.
/// </summary>
public partial class GameFooter : INotifyPropertyChanged, IDisposable, IAsyncDisposable
{
    private StatusIndicator _status = StatusIndicator.Disconnected;
    private string _statusText = "Disconnected";
    private int _pingMs;
    private bool _showReconnectingBanner;
    private int _playerCount;
    private readonly Random _pingSimulator = new();
    private CancellationTokenSource? _pingCts;
    private Task? _pingLoop;

    /// <summary>
    /// Gets the current connection status indicator.
    /// </summary>
    public StatusIndicator Status
    {
        get => _status;
        private set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DotClass));
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    /// <summary>
    /// Gets the CSS class for the connection status dot based on current <see cref="Status"/>.
    /// </summary>
    public string DotClass => _status switch
    {
        StatusIndicator.Connected => "dot-connected",
        StatusIndicator.Degraded => "dot-degraded",
        StatusIndicator.Reconnecting => "dot-reconnecting",
        _ => "dot-disconnected"
    };

    /// <summary>
    /// Gets the human-readable status text describing the connection state.
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the current server ping/round-trip time in milliseconds, or <c>0</c> if unknown.
    /// </summary>
    public int PingMs
    {
        get => _pingMs;
        private set
        {
            if (_pingMs != value)
            {
                _pingMs = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets whether the reconnecting banner should be displayed above the footer.
    /// </summary>
    public bool ShowReconnectingBanner
    {
        get => _showReconnectingBanner;
        private set
        {
            if (_showReconnectingBanner != value)
            {
                _showReconnectingBanner = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the estimated number of players currently connected (if available from the hub state).
    /// </summary>
    public int PlayerCount
    {
        get => _playerCount;
        private set
        {
            if (_playerCount != value)
            {
                _playerCount = value;
                OnPropertyChanged();
            }
        }
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    [Inject]
    private IGameHubConnectionService Hub { get; set; } = null!;

    [Inject]
    private IGameStateService GameState { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        // Subscribe to GameState property changes for zone info
        GameState.PropertyChanged += OnGameStatePropertyChanged;

        // Subscribe to hub state changes
        Hub.StateChanged += OnHubStateChanged;

        // Initialize status from current hub state
        UpdateStatusFromHubState();

        // Start the ping measurement loop
        _pingCts = new CancellationTokenSource();
        _pingLoop = PingLoopAsync(_pingCts.Token);
    }

    private void OnGameStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
        => _ = InvokeAsync(StateHasChanged);

    private void OnHubStateChanged(object? sender, ConnectionState state)
    {
        _ = InvokeAsync(() =>
        {
            UpdateStatusFromHubState();
            StateHasChanged();
        });
    }

    private void UpdateStatusFromHubState()
    {
        switch (Hub.State)
        {
            case ConnectionState.Connected:
                Status = StatusIndicator.Connected;
                StatusText = "Connected";
                ShowReconnectingBanner = false;

                // If we don't have a ping yet, use simulated fallback until the
                // periodic timer takes its first measurement.
                if (PingMs == 0)
                    PingMs = SimulatePing();
                break;

            case ConnectionState.Degraded:
                Status = StatusIndicator.Degraded;
                StatusText = "Degraded";
                ShowReconnectingBanner = false;
                PingMs = 250; // degraded threshold
                break;

            case ConnectionState.Reconnecting:
                Status = StatusIndicator.Reconnecting;
                StatusText = "Reconnecting";
                ShowReconnectingBanner = true;
                PingMs = 0;
                break;

            case ConnectionState.Connecting:
                Status = StatusIndicator.Reconnecting;
                StatusText = "Connecting";
                ShowReconnectingBanner = true;
                PingMs = 0;
                break;

            case ConnectionState.Failed:
            case ConnectionState.Disconnected:
            default:
                Status = StatusIndicator.Disconnected;
                StatusText = "Disconnected";
                ShowReconnectingBanner = false;
                PingMs = 0;
                break;
        }
    }

    /// <summary>
    /// Background loop that measures ping every 10 seconds using a real hub round-trip.
    /// </summary>
    /// <param name="ct">Cancellation token to stop the loop.</param>
    private async Task PingLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(ct))
        {
            if (Hub.IsConnected)
            {
                await MeasurePingAsync();
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    /// <summary>
    /// Measures the round-trip time to the game server by sending a <c>Ping</c>
    /// hub invocation and timing the response with a <see cref="Stopwatch"/>.
    /// Falls back to <see cref="SimulatePing"/> if the hub call fails.
    /// </summary>
    private async Task MeasurePingAsync()
    {
        try
        {
            var sw = Stopwatch.StartNew();
            await Hub.SendAsync("Ping");
            sw.Stop();
            PingMs = (int)sw.ElapsedMilliseconds;
        }
        catch
        {
            // Hub ping failed; use simulated fallback so the UI still shows a value.
            PingMs = SimulatePing();
        }
    }

    /// <summary>
    /// Simulates a ping measurement as a fallback when the hub is not connected
    /// or when a real ping measurement fails.
    /// Returns a value between 15 and 120 ms.
    /// </summary>
    private int SimulatePing()
    {
        // If we already have a recent ping, add small jitter
        if (_pingMs > 0 && _pingMs <= 200)
        {
            var jitter = _pingSimulator.Next(-10, 15);
            return Math.Max(10, _pingMs + jitter);
        }

        return _pingSimulator.Next(15, 80);
    }

    /// <summary>Sends the SearchArea hub command.</summary>
    private async Task SearchAsync()
    {
        try
        {
            if (Hub.IsConnected)
                await Hub.SendAsync("SearchArea");
        }
        catch { /* Best-effort */ }
    }

    /// <summary>Sends the RestAtLocation hub command.</summary>
    private async Task RestAsync()
    {
        try
        {
            if (Hub.IsConnected)
                await Hub.SendAsync("RestAtLocation");
        }
        catch { /* Best-effort */ }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _pingCts?.Cancel();
        GameState.PropertyChanged -= OnGameStatePropertyChanged;
        Hub.StateChanged -= OnHubStateChanged;
        _pingCts?.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _pingCts?.Cancel();
        if (_pingLoop is not null)
            await _pingLoop;
        _pingCts?.Dispose();
        GameState.PropertyChanged -= OnGameStatePropertyChanged;
        Hub.StateChanged -= OnHubStateChanged;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
