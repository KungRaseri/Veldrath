using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using MudBlazor;
using Veldrath.GameClient.Components.Components.Pages;
using Veldrath.GameClient.Components.Models;
using Veldrath.GameClient.Core.Abstractions;
using Veldrath.GameClient.Core.Services;

namespace Veldrath.GameClient.Components.Components.Layout;

/// <summary>
/// Code-behind for the <see cref="LeftSidebar"/> component.
/// Manages channel pill state, whisper prefix parsing, and filtered message display.
/// </summary>
public partial class LeftSidebar
{
    /// <summary>Callback invoked when the player wants to open an overlay panel.</summary>
    [Parameter] public EventCallback<GameOverlay.OverlayKind> OnOpenOverlay { get; set; }

    /// <summary>
    /// Gets or sets the main content rendered between the left and right sidebars.
    /// Must be provided by the parent page (e.g., <see cref="Game"/> wraps its
    /// <c>MudContainer</c> inside <c>Sidebars</c> tags).
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private IDisposable? _stateSubscription;
    private bool _disposed;

    private IReadOnlyList<EntityListItem> OccupantItems =>
        GameState.ZoneOccupants
            .Select(occupant => new EntityListItem(
                occupant.CharacterId.ToString(),
                occupant.CharacterName,
                $"Entered {occupant.EnteredAt.ToLocalTime():g}",
                Icons.Material.Filled.Person))
            .ToList();

    private IReadOnlyList<EntityListItem> EnemyItems =>
        GameState.ZoneEnemies
            .Select(enemy => new EntityListItem(
                enemy.EnemyId.ToString(),
                enemy.EnemyName,
                $"Level {enemy.Level} · HP {enemy.CurrentHealth}/{enemy.MaxHealth}",
                Icons.Material.Filled.BugReport))
            .ToList();

    private IReadOnlyList<EntityListItem> KnownLocationItems =>
        GameState.KnownLocations
            .Select(location => new EntityListItem(
                location.Slug,
                location.DisplayName,
                location.TypeKey,
                Icons.Material.Filled.Place,
                location.Slug == GameState.CurrentZoneLocationSlug ? "Here" : null,
                IsSelected: location.Slug == GameState.CurrentZoneLocationSlug,
                IsDisabled: !Hub.IsConnected ||
                            GameState.CurrentCharacterId is null ||
                            location.Slug == GameState.CurrentZoneLocationSlug))
            .ToList();

    private IReadOnlyList<EntityListItem> ConnectionItems =>
        GameState.CurrentLocationConnections
            .Select(connection => new EntityListItem(
                connection.Slug,
                connection.Slug,
                connection.IsCrossZone ? "Zone transition" : connection.TargetZoneId,
                Icons.Material.Filled.AltRoute,
                IsDisabled: !Hub.IsConnected || GameState.CurrentCharacterId is null))
            .ToList();

    private Task OnLocationSelected(EntityListItem item) =>
        NavigateToLocationAsync(item.Id);

    /// <summary>G68: Sends the VisitShop hub command with the current zone ID.</summary>
    private async Task VisitShopAsync()
    {
        try
        {
            if (GameState.CurrentCharacterId is null)
            {
                GameState.ApplySystemMessage("Please wait — your character session is being restored.");
                return;
            }

            if (Hub.IsConnected)
                await Hub.SendAsync("VisitShop", new { ZoneId = GameState.CurrentZoneId });
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to visit shop.");
        }
    }

    /// <summary>G70: Sends the SearchArea hub command to search the current tile.</summary>
    private async Task SearchAreaAsync()
    {
        try
        {
            if (GameState.CurrentCharacterId is null)
            {
                GameState.ApplySystemMessage("Please wait — your character session is being restored.");
                return;
            }

            if (Hub.IsConnected)
                await Hub.SendAsync("SearchArea");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to search area.");
        }
    }

    /// <summary>Leaves the current zone and reloads the region map graph.</summary>
    private async Task LeaveZoneAsync()
    {
        try
        {
            if (GameState.CurrentCharacterId is null)
            {
                GameState.ApplySystemMessage("Please wait — your character session is being restored.");
                return;
            }

            if (!Hub.IsConnected)
                return;

            await Hub.SendAsync("LeaveZone");
            await Hub.RequestMapGraphAsync();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to leave zone.");
        }
    }

    /// <summary>G75: Sends the RestAtLocation hub command with the current location and cost.</summary>
    private async Task RestAtLocationAsync()
    {
        try
        {
            if (GameState.CurrentCharacterId is null)
            {
                GameState.ApplySystemMessage("Please wait — your character session is being restored.");
                return;
            }

            if (Hub.IsConnected)
            {
                await Hub.SendAsync("RestAtLocation", new
                {
                    LocationId = GetCurrentLocationId(),
                    CostInGold = 10,
                });
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to rest.");
        }
    }

    private string GetCurrentLocationId() =>
        !string.IsNullOrWhiteSpace(GameState.CurrentZoneLocationSlug)
            ? GameState.CurrentZoneLocationSlug!
            : GameState.CurrentZoneId ?? string.Empty;

    /// <summary>G76: Sends the AllocateAttributePoints hub command with a default Strength allocation.</summary>
    private async Task AllocateStrengthAsync()
    {
        try
        {
            if (GameState.CurrentCharacterId is null)
            {
                GameState.ApplySystemMessage("Please wait — your character session is being restored.");
                return;
            }

            if (Hub.IsConnected)
            {
                var allocation = new Dictionary<string, int>
                {
                    { "Strength", 1 },
                };
                await Hub.SendAsync("AllocateAttributePoints", allocation);
                Logger.LogInformation("Allocated 1 point to Strength.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to allocate attribute points.");
        }
    }

    /// <summary>G81: Sends the NavigateToLocation hub command with the target location slug.</summary>
    /// <param name="slug">The target location slug.</param>
    private async Task NavigateToLocationAsync(string slug)
    {
        if (string.IsNullOrEmpty(slug))
            return;

        if (GameState.CurrentCharacterId is null)
        {
            GameState.ApplySystemMessage("Please wait — your character session is being restored.");
            return;
        }

        try
        {
            if (Hub.IsConnected)
            {
                await Hub.SendAsync("NavigateToLocation", new { LocationSlug = slug });
                Logger.LogInformation("Navigating to location: {LocationSlug}", slug);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to navigate to location {LocationSlug}.", slug);
        }
    }

    [Inject]
    private IGameHubConnectionService Hub { get; set; } = null!;

    [Inject]
    private IGameStateService GameState { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        _stateSubscription = GameState.OnStateChanged(() => InvokeAsync(StateHasChanged));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stateSubscription?.Dispose();
    }

    /// <summary>
    /// Simple disposable that invokes an action on dispose.
    /// </summary>
    private sealed class Subscription(Action onDispose) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose() => onDispose();
    }
}
