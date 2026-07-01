using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Veldrath.Contracts.Connection;
using Veldrath.Contracts.Tilemap;
using Veldrath.GameClient.Core.Abstractions;
using Veldrath.GameClient.Core.Payloads;
using Veldrath.GameClient.Core.Services;
using Veldrath.Web.Services;
// Aliases to resolve type name conflicts with Veldrath.Contracts types.
using GameChatMessagePayload = Veldrath.GameClient.Core.Payloads.ChatMessageHubDto;

namespace Veldrath.Web.Components.Pages.Game;

/// <summary>
/// Main game hub page — orchestrates all game sub-components, manages the SignalR
/// hub connection, and dispatches server events to <see cref="IGameStateService"/>.
/// </summary>
public sealed partial class Game : IAsyncDisposable
{
    [Inject] private IGameHubConnectionService Hub { get; set; } = null!;
    [Inject] private IGameStateService GameState { get; set; } = null!;
    [Inject] private AuthStateService Auth { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [Inject] private IConfiguration Configuration { get; set; } = null!;
    [Inject] private ILogger<Game> Logger { get; set; } = null!;

    private readonly List<IDisposable> _hubSubscriptions = [];
    private IDisposable? _stateSubscription;
    private bool _hubConnected;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        if (!Auth.IsLoggedIn)
        {
            Navigation.NavigateTo("/Login");
            return;
        }

        await ConnectAndRegisterHandlers();
    }

    private async Task ConnectAndRegisterHandlers()
    {
        try
        {
            // Ensure the JWT is still fresh.
            var tokenValid = await Auth.TryRefreshAsync();
            if (!tokenValid || Auth.AccessToken is null)
            {
                Navigation.NavigateTo("/Login");
                return;
            }

            var serverUrl = Configuration["Veldrath:ServerUrl"]
                ?? throw new InvalidOperationException("Veldrath:ServerUrl is not configured.");

            // Always register hub event handlers, regardless of whether the hub is
            // already connected from CharacterSelect.  If we skip registration when
            // already connected, events like ZoneEntered / ZoneTileMap will never be
            // handled because the CharacterSelect page only registered its own handlers.
            RegisterHubHandlers();

            // Use OnStateChanged via the concrete GameStateService since it has the method.
            // IGameStateService only provides INotifyPropertyChanged.
            if (GameState is GameStateService gs)
            {
                _stateSubscription ??= gs.OnStateChanged(() => InvokeAsync(StateHasChanged));
            }

            if (!Hub.IsConnected)
            {
                await Hub.ConnectAsync(serverUrl, Auth.AccessToken);
            }

            _hubConnected = Hub.IsConnected;

            // If the hub is now connected and we have a character, enter the zone.
            if (_hubConnected && GameState.CurrentCharacterId is not null)
            {
                await EnterZoneAfterConnectAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to connect to game hub.");
            if (GameState is GameStateService gs)
            {
                gs.ApplySystemMessage("Failed to connect to game server.");
            }
        }
    }

    /// <summary>
    /// Enters the character's last-known zone and requests the tile map.
    /// Called after the hub connection is established (or found already connected).
    /// </summary>
    private async Task EnterZoneAfterConnectAsync()
    {
        // If we already have a zone tilemap, nothing to do.
        if (GameState.ZoneTileMap is not null)
            return;

        var zoneId = GameState.CurrentZoneId;

        // If we don't have a zone ID yet, request re-selection from the server.
        // The CharacterSelected handler will fire and we can proceed from there.
        if (string.IsNullOrEmpty(zoneId))
        {
            Logger.LogInformation(
                "No zone ID known yet — re-selecting character to obtain zone info.");

            try
            {
                var charId = GameState.CurrentCharacterId;
                if (charId is not null && Guid.TryParse(charId, out var guid))
                {
                    await Hub.SendAsync("SelectCharacter", guid);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to re-select character after connecting.");
                if (GameState is GameStateService gs)
                {
                    gs.ApplySystemMessage("Failed to restore game session.");
                }
            }
            return;
        }

        await EnterZoneAsync(zoneId);
    }

    /// <summary>Enters the specified zone and requests the tile map.</summary>
    /// <param name="zoneId">The zone to enter.</param>
    private async Task EnterZoneAsync(string zoneId)
    {
        Logger.LogInformation("Entering zone {ZoneId}...", zoneId);

        try
        {
            await Hub.SendAsync("EnterZone", zoneId);
            await Hub.SendAsync("GetZoneTileMap");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to enter zone {ZoneId}.", zoneId);
            if (GameState is GameStateService gs)
            {
                gs.ApplySystemMessage("Failed to enter zone.");
            }
        }
    }

    private void RegisterHubHandlers()
    {
        // ServerInfo — store connection ID.
        _hubSubscriptions.Add(Hub.On<ServerInfoPayload>("ServerInfo", async payload =>
        {
            if (GameState is GameStateService gs)
            {
                gs.ApplyServerInfo(payload.ConnectionId);
            }
            await InvokeAsync(StateHasChanged);
        }));

        // CharacterSelected — update character state.
        _hubSubscriptions.Add(Hub.On<CharacterSelectedPayload>("CharacterSelected", async payload =>
        {
            GameState.ApplyCharacterSelected(payload);

            // If we now have a zone ID and the tilemap hasn't loaded yet, enter the zone.
            if (!string.IsNullOrEmpty(payload.CurrentZoneId) && GameState.ZoneTileMap is null)
            {
                await EnterZoneAsync(payload.CurrentZoneId);
            }

            await InvokeAsync(StateHasChanged);
        }));

        // ZoneEntered — update zone state.
        _hubSubscriptions.Add(Hub.On<ZoneEnteredPayload>("ZoneEntered", async payload =>
        {
            GameState.ApplyZoneEntered(payload);
            await InvokeAsync(StateHasChanged);
        }));

        // ZoneTileMap — store tile map data.
        _hubSubscriptions.Add(Hub.On<TileMapDto>("ZoneTileMap", async dto =>
        {
            var tiles = ConvertTileMap(dto);
            if (GameState is GameStateService gs)
            {
                gs.ApplyZoneTileMap(tiles);
            }
            await InvokeAsync(StateHasChanged);
        }));

        // CharacterMoved — update player or other character position.
        // Use Veldrath.Contracts.Tilemap.CharacterMovedPayload (shared with server).
        _hubSubscriptions.Add(Hub.On<Veldrath.Contracts.Tilemap.CharacterMovedPayload>("CharacterMoved", async payload =>
        {
            GameState.ApplyCharacterMoved(payload);
            await InvokeAsync(StateHasChanged);
        }));

        // PlayerEntered — occupant joined the zone.
        _hubSubscriptions.Add(Hub.On<PlayerEnteredPayload>("PlayerEntered", async payload =>
        {
            GameState.ApplyPlayerEntered(payload);
            await InvokeAsync(StateHasChanged);
        }));

        // PlayerLeft — occupant left the zone.
        _hubSubscriptions.Add(Hub.On<PlayerLeftPayload>("PlayerLeft", async payload =>
        {
            GameState.ApplyPlayerLeft(payload);
            await InvokeAsync(StateHasChanged);
        }));

        // CombatStarted — engage enemy.
        _hubSubscriptions.Add(Hub.On<CombatStartedPayload>("CombatStarted", async payload =>
        {
            GameState.ApplyCombatStarted(payload);
            await InvokeAsync(StateHasChanged);
        }));

        // CombatTurn — combat action result.
        _hubSubscriptions.Add(Hub.On<CombatTurnPayload>("CombatTurn", async payload =>
        {
            GameState.ApplyCombatTurn(payload);
            await InvokeAsync(StateHasChanged);
        }));

        // CombatEnded — combat finished (fled, won, or death).
        _hubSubscriptions.Add(Hub.On<CombatEndedPayload>("CombatEnded", async payload =>
        {
            GameState.ApplyCombatEnded(payload);
            await InvokeAsync(StateHasChanged);
        }));

        // EnemyDefeated — an enemy was killed.
        _hubSubscriptions.Add(Hub.On<EnemyDefeatedPayload>("EnemyDefeated", async payload =>
        {
            GameState.ApplyEnemyDefeated(payload);
            await InvokeAsync(StateHasChanged);
        }));

        // CharacterRespawned — character respawned after death.
        _hubSubscriptions.Add(Hub.On<CharacterRespawnedPayload>("CharacterRespawned", async payload =>
        {
            if (GameState is GameStateService gs)
            {
                gs.ApplySystemMessage("You have respawned.");
            }
            await InvokeAsync(StateHasChanged);
        }));

        // ReceiveChatMessage — incoming chat message.
        _hubSubscriptions.Add(Hub.On<GameChatMessagePayload>("ReceiveChatMessage", async payload =>
        {
            GameState.ApplyChatMessage(payload);
            await InvokeAsync(StateHasChanged);
        }));

        // SystemMessage — server system message.
        _hubSubscriptions.Add(Hub.On<string>("SystemMessage", async message =>
        {
            if (GameState is GameStateService gs)
            {
                gs.ApplySystemMessage(message);
            }
            await InvokeAsync(StateHasChanged);
        }));

        // Error — display server errors as system messages.
        _hubSubscriptions.Add(Hub.On<string>("Error", async error =>
        {
            Logger.LogWarning("Hub error: {Error}", error);
            if (GameState is GameStateService gs)
            {
                gs.ApplySystemMessage($"Error: {error}");
            }
            await InvokeAsync(StateHasChanged);
        }));

        // ZoneEntitiesSnapshot — update occupants and enemies with positions.
        // Use Veldrath.Contracts.Tilemap.ZoneEntitiesSnapshotPayload (shared with server).
        _hubSubscriptions.Add(Hub.On<Veldrath.Contracts.Tilemap.ZoneEntitiesSnapshotPayload>("ZoneEntitiesSnapshot", async payload =>
        {
            GameState.ApplyZoneEntitiesSnapshot(payload);
            await InvokeAsync(StateHasChanged);
        }));
    }

    /// <summary>
    /// Converts a <see cref="TileMapDto"/> from the server into the local <see cref="Tile"/> array
    /// used by the <see cref="GameStateService"/>.
    /// </summary>
    private static Tile[,] ConvertTileMap(TileMapDto dto)
    {
        var width = dto.Width;
        var height = dto.Height;
        var tiles = new Tile[height, width];

        // Use the first ground layer for tile types, or default to grass (type 0).
        var groundLayer = dto.Layers.FirstOrDefault();

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var idx = y * width + x;
                var tileType = groundLayer is not null && idx < groundLayer.Data.Length
                    ? groundLayer.Data[idx]
                    : 0;
                var isBlocked = dto.CollisionMask is not null && idx < dto.CollisionMask.Length && dto.CollisionMask[idx];

                // Map from tile index to our simplified type system:
                // -1 = void, 0 = grass (walkable), positive = various terrain.
                var simplifiedType = tileType switch
                {
                    -1 => -1,     // void
                    0 => 0,       // grass / walkable
                    1 => 1,       // wall
                    2 => 2,       // water
                    3 => 3,       // door
                    _ when isBlocked => 1, // any blocked tile → wall
                    _ => 0        // default walkable
                };

                tiles[y, x] = new Tile(x, y, simplifiedType, isBlocked);
            }
        }

        return tiles;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _stateSubscription?.Dispose();

        foreach (var sub in _hubSubscriptions)
        {
            sub.Dispose();
        }
        _hubSubscriptions.Clear();

        if (_hubConnected)
        {
            try { await Hub.DisconnectAsync(); }
            catch (Exception ex) { Logger.LogWarning(ex, "Error disconnecting hub."); }
        }
    }
}

// ── CharacterRespawned Payload (not yet in shared payloads) ───────────────────

/// <summary>
/// Hub event payload received when a character respawns after death.
/// Contains the character's updated health and mana.
/// </summary>
/// <param name="CharacterId">The respawned character's identifier.</param>
/// <param name="CurrentHealth">The character's health after respawn.</param>
/// <param name="CurrentMana">The character's mana after respawn.</param>
public sealed record CharacterRespawnedPayload(Guid CharacterId, int CurrentHealth, int CurrentMana);
