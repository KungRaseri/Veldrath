using Microsoft.AspNetCore.Components;
using Veldrath.Auth.Blazor;
using Veldrath.Contracts.Tilemap;
using Veldrath.GameClient.Components.Models;
using Veldrath.GameClient.Core.Abstractions;

namespace Veldrath.GameClient.Components.Components.Pages;

/// <summary>
/// Code-behind for the <see cref="GameMap"/> component.
/// Manages the region map lifecycle: requesting zone data via hub, handling the response,
/// and providing navigation to zones.
/// </summary>
public partial class GameMap : IDisposable
{
    private readonly List<ZoneNode> _zones = [];
    private string _regionName = string.Empty;
    private bool _isLoading = true;
    private string? _errorMessage;
    private bool _showLegend = true;
    private string _targetRegionId = string.Empty;
    private IDisposable? _regionMapSubscription;
    private IDisposable? _stateSubscription;

    /// <summary>
    /// Gets the display name of the current region, or a default string if unknown.
    /// </summary>
    public string RegionName
    {
        get => string.IsNullOrEmpty(_regionName) ? "Unknown Region" : _regionName;
        private set => _regionName = value;
    }

    [Inject]
    private IGameHubConnectionService Hub { get; set; } = null!;

    [Inject]
    private IGameStateService GameState { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private AuthStateServiceBase Auth { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Auth.OnChange += OnAuthStateChanged;

        if (!Auth.IsAuthReady)
            return;

        if (!Auth.IsLoggedIn)
        {
            Navigation.NavigateTo("/login");
            return;
        }

        _stateSubscription = GameState.OnStateChanged(() => InvokeAsync(StateHasChanged));

        // Subscribe to the RegionMap hub event before sending the request,
        // so we don't miss the response.
        _regionMapSubscription = Hub.On<RegionMapDto>("RegionMap", OnRegionMapReceived);

        // Request the region map from the server.
        _ = LoadRegionMapAsync();
    }

    private void OnAuthStateChanged()
    {
        if (Auth.IsAuthReady)
            InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Sends the <c>GetRegionMap</c> hub command to request the current region's zone data.
    /// </summary>
    private async Task LoadRegionMapAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        StateHasChanged();

        try
        {
            if (Hub.IsConnected)
            {
                await Hub.SendAsync("GetRegionMap");
            }
            else
            {
                _errorMessage = "Not connected to game server.";
                _isLoading = false;
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load region map: {ex.Message}";
            _isLoading = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Handles the <c>RegionMap</c> hub event containing zone entry data.
    /// </summary>
    /// <param name="dto">The region map data from the server.</param>
    private Task OnRegionMapReceived(RegionMapDto dto)
    {
        _zones.Clear();

        // Determine the current zone ID from game state for highlighting.
        var currentZoneId = GameState.CurrentZoneId ?? string.Empty;

        // Build zone nodes from the region map's zone entries.
        // In a full implementation the server would provide discovered/unlocked status
        // per character; for now we treat all zone entries as discovered.
        foreach (var entry in dto.ZoneEntries)
        {
            var isCurrent = string.Equals(entry.ZoneSlug, currentZoneId, StringComparison.OrdinalIgnoreCase);
            _zones.Add(new ZoneNode(
                entry.ZoneSlug,
                entry.DisplayName,
                "wilderness",
                entry.MinLevel,
                isCurrent,
                IsDiscovered: true));
        }

        // If no zone entries came from the server, populate with fallback data
        // from the game state so the map is never completely empty.
        if (_zones.Count == 0 && !string.IsNullOrEmpty(GameState.CurrentZoneName))
        {
            _zones.Add(new ZoneNode(
                GameState.CurrentZoneId ?? "unknown",
                GameState.CurrentZoneName,
                "town",
                1,
                true,
                true));
        }

        RegionName = dto.RegionId;
        _isLoading = false;
        _errorMessage = null;
        StateHasChanged();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a <c>MoveOnRegion</c> hub command to navigate to the specified zone.
    /// </summary>
    /// <param name="zoneId">The target zone identifier.</param>
    private async Task NavigateToZone(string zoneId)
    {
        if (string.IsNullOrEmpty(zoneId))
            return;

        try
        {
            if (Hub.IsConnected)
            {
                await Hub.SendAsync("MoveOnRegion", zoneId);
            }
        }
        catch (Exception)
        {
            // Navigation failure is handled silently — the server will
            // send an error via the SystemMessage channel if applicable.
        }
    }

    /// <summary>
    /// Retries loading the region map after an error.
    /// </summary>
    private async Task LoadRegionMap()
    {
        await LoadRegionMapAsync();
    }

    /// <summary>
    /// G73: Sends the <c>ChangeRegion</c> hub command to travel to a different region.
    /// </summary>
    private async Task ChangeRegionAsync()
    {
        var target = _targetRegionId?.Trim();
        if (string.IsNullOrEmpty(target))
            return;

        try
        {
            if (Hub.IsConnected)
            {
                await Hub.SendAsync("ChangeRegion", target);
                _targetRegionId = string.Empty;
            }
        }
        catch (Exception)
        {
            // Region change failure is handled silently — the server will
            // send an error via the SystemMessage channel if applicable.
        }
    }

    /// <summary>
    /// Navigates back to the main game view.
    /// </summary>
    private void GoBack()
    {
        Navigation.NavigateTo("/Game/Play");
    }

    /// <summary>
    /// Gets the CSS class for a zone card based on its state.
    /// </summary>
    /// <param name="zone">The zone node.</param>
    /// <returns>A space-separated CSS class string.</returns>
    private static string GetZoneCardClass(ZoneNode zone)
    {
        if (zone.IsCurrent) return "zone-card-current";
        if (!zone.IsDiscovered) return "zone-card-locked";
        return "zone-card-available";
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Auth.OnChange -= OnAuthStateChanged;
        _regionMapSubscription?.Dispose();
        _stateSubscription?.Dispose();
    }
}
