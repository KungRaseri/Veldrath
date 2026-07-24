using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Veldrath.GameClient.Core.Abstractions;

namespace Veldrath.GameClient.Components.Components.Pages;

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


    /// <summary>G68: Sends the VisitShop hub command to open the merchant interface.</summary>
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
                await Hub.SendAsync("VisitShop");
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

    /// <summary>G74: Sends the ExitZone hub command to leave the current zone.</summary>
    private async Task ExitZoneAsync()
    {
        try
        {
            if (GameState.CurrentCharacterId is null)
            {
                GameState.ApplySystemMessage("Please wait — your character session is being restored.");
                return;
            }

            if (Hub.IsConnected)
                await Hub.SendAsync("ExitZone");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to exit zone.");
        }
    }

    /// <summary>G75: Sends the RestAtLocation hub command to rest at an inn.</summary>
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
                await Hub.SendAsync("RestAtLocation");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to rest.");
        }
    }

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

    /// <summary>G81: Sends the NavigateToLocation hub command to move to a zone location.</summary>
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
                await Hub.SendAsync("NavigateToLocation", slug);
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
