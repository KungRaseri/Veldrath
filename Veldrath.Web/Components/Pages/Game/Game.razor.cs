using System.ComponentModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Veldrath.Web.Services;
using Veldrath.Contracts.Connection;
using Veldrath.Contracts.Tilemap;

namespace Veldrath.Web.Components.Pages.Game;

/// <summary>
/// Main game hub page — orchestrates all game sub-components, manages the SignalR
/// hub connection, and dispatches server events to <see cref="GameStateService"/>.
/// </summary>
public sealed partial class Game : IAsyncDisposable
{
    [Inject] private GameHubConnectionService Hub { get; set; } = null!;
    [Inject] private GameStateService GameState { get; set; } = null!;
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

            // If already connected from CharacterSelect, skip reconnecting.
            if (!Hub.IsConnected)
            {
                // Register all hub event handlers BEFORE connecting.
                RegisterHubHandlers();

                await Hub.ConnectAsync(serverUrl, Auth.AccessToken);

                // Subscribe to GameState property changes to trigger UI re-renders.
                _stateSubscription = GameState.OnStateChanged(() => InvokeAsync(StateHasChanged));
            }

            _hubConnected = true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to connect to game hub.");
            GameState.ApplySystemMessage("Failed to connect to game server.");
        }
    }

    private void RegisterHubHandlers()
    {
        // ServerInfo — store connection ID.
        _hubSubscriptions.Add(Hub.On<ServerInfoPayload>("ServerInfo", async payload =>
        {
            GameState.ApplyServerInfo(payload.ConnectionId);
            await InvokeAsync(StateHasChanged);
        }));

        // CharacterSelected — update character state.
        _hubSubscriptions.Add(Hub.On<CharacterSelectedPayload>("CharacterSelected", async payload =>
        {
            var charInfo = new Services.CharacterBasicInfo(
                payload.Id, payload.Name, payload.ClassName, payload.Level,
                payload.Experience, payload.CurrentHealth, payload.MaxHealth,
                payload.CurrentMana, payload.MaxMana, payload.Gold);
            GameState.ApplyCharacterSelected(charInfo);
            await InvokeAsync(StateHasChanged);
        }));

        // ZoneEntered — update zone state.
        _hubSubscriptions.Add(Hub.On<ZoneEnteredPayload>("ZoneEntered", async payload =>
        {
            // We don't have the tilemap yet; the server sends it separately via ZoneTileMap.
            GameState.ApplyZoneEntered(payload.Id, payload.Name, 0, 0, null);
            await InvokeAsync(StateHasChanged);
        }));

        // ZoneTileMap — store tile map data.
        _hubSubscriptions.Add(Hub.On<TileMapDto>("ZoneTileMap", async dto =>
        {
            var tiles = ConvertTileMap(dto);
            GameState.ApplyZoneTileMap(tiles);
            await InvokeAsync(StateHasChanged);
        }));

        // CharacterMoved — update player or other character position.
        _hubSubscriptions.Add(Hub.On<CharacterMovedPayload>("CharacterMoved", async payload =>
        {
            if (GameState.CurrentCharacter is not null && payload.CharacterId == GameState.CurrentCharacter.Id)
            {
                GameState.ApplyCharacterMoved(payload.TileX, payload.TileY);
            }
            await InvokeAsync(StateHasChanged);
        }));

        // PlayerEntered — occupant joined the zone.
        _hubSubscriptions.Add(Hub.On<PlayerEnteredPayload>("PlayerEntered", async payload =>
        {
            var occupant = new OccupantInfo(payload.CharacterId, payload.CharacterName, DateTimeOffset.UtcNow);
            GameState.ApplyPlayerEntered(occupant);
            await InvokeAsync(StateHasChanged);
        }));

        // PlayerLeft — occupant left the zone.
        _hubSubscriptions.Add(Hub.On<PlayerLeftPayload>("PlayerLeft", async payload =>
        {
            GameState.ApplyPlayerLeft(payload.CharacterId);
            await InvokeAsync(StateHasChanged);
        }));

        // CombatStarted — engage enemy.
        _hubSubscriptions.Add(Hub.On<CombatStartedPayload>("CombatStarted", async payload =>
        {
            var enemy = new EnemyInfo(
                payload.EnemyId, payload.EnemyName, payload.EnemyLevel,
                payload.EnemyCurrentHealth, payload.EnemyMaxHealth, 0, 0);
            GameState.ApplyCombatStarted(enemy);
            await InvokeAsync(StateHasChanged);
        }));

        // CombatTurn — combat action result.
        _hubSubscriptions.Add(Hub.On<CombatTurnPayload>("CombatTurn", async payload =>
        {
            var resultText = BuildCombatResultText(payload);
            GameState.ApplyCombatTurn(resultText);

            // Update character HP from the turn result.
            if (GameState.CurrentCharacter is not null)
            {
                var updated = GameState.CurrentCharacter with
                {
                    CurrentHealth = payload.PlayerRemainingHealth
                };
                GameState.ApplyCharacterSelected(updated);
            }

            await InvokeAsync(StateHasChanged);
        }));

        // CombatEnded — combat finished (fled, won, or death).
        _hubSubscriptions.Add(Hub.On<CombatEndedPayload>("CombatEnded", async payload =>
        {
            GameState.ApplyCombatEnded();
            GameState.ApplySystemMessage($"Combat ended: {payload.Reason}");
            await InvokeAsync(StateHasChanged);
        }));

        // EnemyDefeated — an enemy was killed.
        _hubSubscriptions.Add(Hub.On<EnemyDefeatedPayload>("EnemyDefeated", async payload =>
        {
            // The GameState.ApplyEnemyDefeated needs enemyName/xpGained.
            // Since this broadcast only has CharacterId, show a generic message.
            GameState.ApplySystemMessage("An enemy has been defeated!");
            await InvokeAsync(StateHasChanged);
        }));

        // CharacterRespawned — character respawned after death.
        _hubSubscriptions.Add(Hub.On<CharacterRespawnedPayload>("CharacterRespawned", async payload =>
        {
            if (GameState.CurrentCharacter is not null)
            {
                var updated = GameState.CurrentCharacter with
                {
                    CurrentHealth = payload.CurrentHealth,
                    CurrentMana = payload.CurrentMana
                };
                GameState.ApplyCharacterSelected(updated);
            }
            GameState.ApplyCombatEnded();
            GameState.ApplySystemMessage("You have respawned.");
            await InvokeAsync(StateHasChanged);
        }));

        // ReceiveChatMessage — incoming chat message.
        _hubSubscriptions.Add(Hub.On<ChatMessageHubDto>("ReceiveChatMessage", async payload =>
        {
            var msg = new Services.ChatMessage(
                payload.CharacterId, payload.Channel, payload.Sender,
                payload.Message, payload.Timestamp);
            GameState.ApplyChatMessage(msg);
            await InvokeAsync(StateHasChanged);
        }));

        // SystemMessage — server system message.
        _hubSubscriptions.Add(Hub.On<string>("SystemMessage", async message =>
        {
            GameState.ApplySystemMessage(message);
            await InvokeAsync(StateHasChanged);
        }));

        // Error — display server errors as system messages.
        _hubSubscriptions.Add(Hub.On<string>("Error", async error =>
        {
            Logger.LogWarning("Hub error: {Error}", error);
            GameState.ApplySystemMessage($"Error: {error}");
            await InvokeAsync(StateHasChanged);
        }));

        // ZoneEntitiesSnapshot — update occupants and enemies with positions.
        _hubSubscriptions.Add(Hub.On<ZoneEntitiesSnapshotPayload>("ZoneEntitiesSnapshot", async payload =>
        {
            var currentCharId = GameState.CurrentCharacter?.Id ?? Guid.Empty;

            // Update occupant positions from the snapshot.
            foreach (var entity in payload.Entities.Where(e => e.EntityType == "player"))
            {
                if (entity.EntityId != currentCharId)
                {
                    GameState.ApplyPlayerPositioned(entity.EntityId, entity.SpriteKey, entity.TileX, entity.TileY);
                }
            }

            // Build enemy list from the snapshot (keep existing enemies if not in snapshot).
            var enemies = payload.Entities
                .Where(e => e.EntityType == "enemy")
                .Select(e => new EnemyInfo(e.EntityId, e.SpriteKey, 1, 10, 10, e.TileX, e.TileY))
                .ToList();

            // Only replace enemies if the snapshot contains them.
            if (enemies.Count > 0 || payload.Entities.Count == 0)
            {
                GameState.ApplyZoneEntitiesSnapshot(GameState.ZoneOccupants, enemies);
            }

            await InvokeAsync(StateHasChanged);
        }));
    }

    /// <summary>
    /// Converts a <see cref="TileMapDto"/> from the server into the local <see cref="Tile"/> array
    /// used by the <see cref="GameStateService"/>.
    /// </summary>
    private static Services.Tile[,] ConvertTileMap(TileMapDto dto)
    {
        var width = dto.Width;
        var height = dto.Height;
        var tiles = new Services.Tile[height, width];

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

                tiles[y, x] = new Services.Tile(x, y, simplifiedType, isBlocked);
            }
        }

        return tiles;
    }

    /// <summary>Builds a human-readable combat result string from the turn payload.</summary>
    private static string BuildCombatResultText(CombatTurnPayload p)
    {
        var parts = new List<string>();

        if (p.Action == "attack" && p.PlayerDamage > 0)
            parts.Add($"You hit for {p.PlayerDamage} damage.");
        else if (p.Action == "attack")
            parts.Add("Your attack missed.");

        if (p.Action == "defend")
            parts.Add("You brace for impact.");

        if (p.Action == "ability" && p.AbilityDamage > 0)
            parts.Add($"Your ability dealt {p.AbilityDamage} damage.");
        if (p.Action == "ability" && p.HealthRestored > 0)
            parts.Add($"You restored {p.HealthRestored} health.");

        if (p.EnemyDamage > 0)
            parts.Add(string.IsNullOrEmpty(p.EnemyAbilityUsed)
                ? $"Enemy hits you for {p.EnemyDamage} damage."
                : $"Enemy uses {p.EnemyAbilityUsed} for {p.EnemyDamage} damage.");
        else if (p.Action == "defend")
            parts.Add("You blocked the enemy attack.");

        if (p.EnemyDefeated)
            parts.Add("Enemy defeated!");

        if (p.PlayerDefeated)
            parts.Add("You have been defeated!");

        if (p.XpEarned > 0)
            parts.Add($"Gained {p.XpEarned} XP.");
        if (p.GoldEarned > 0)
            parts.Add($"Gained {p.GoldEarned} gold.");

        return parts.Count > 0 ? string.Join(" ", parts) : "Combat turn processed.";
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

    // ── Hub Event Payload DTOs (matching server's anonymous types) ──────────

    /// <summary>Payload for <c>CharacterSelected</c> hub event.</summary>
    private sealed record CharacterSelectedPayload(
        Guid Id, string Name, string ClassName, int Level, long Experience,
        string? CurrentZoneId, string RegionId, int CurrentHealth, int MaxHealth,
        int CurrentMana, int MaxMana, int Gold, int UnspentAttributePoints,
        int Strength, int Dexterity, int Constitution, int Intelligence,
        int Wisdom, int Charisma, List<string> LearnedAbilities, DateTimeOffset SelectedAt);

    /// <summary>Payload for <c>ZoneEntered</c> hub event.</summary>
    private sealed record ZoneEnteredPayload(string Id, string Name, string Description, string ZoneType, IReadOnlyList<OccupantEntry> Occupants);

    /// <summary>A single occupant entry within the <see cref="ZoneEnteredPayload"/>.</summary>
    private sealed record OccupantEntry(Guid CharacterId, string CharacterName, DateTimeOffset EnteredAt);

    /// <summary>Payload for <c>PlayerEntered</c> hub event.</summary>
    private sealed record PlayerEnteredPayload(Guid CharacterId, string CharacterName, string ZoneId);

    /// <summary>Payload for <c>PlayerLeft</c> hub event.</summary>
    private sealed record PlayerLeftPayload(Guid CharacterId, string CharacterName, string ZoneId);

    /// <summary>Payload for <c>CombatStarted</c> hub event.</summary>
    private sealed record CombatStartedPayload(
        Guid CharacterId, Guid EnemyId, string EnemyName, int EnemyLevel,
        int EnemyCurrentHealth, int EnemyMaxHealth, List<string> EnemyAbilityNames);

    /// <summary>Payload for <c>CombatTurn</c> hub event.</summary>
    private sealed record CombatTurnPayload(
        string Action, int PlayerDamage, int EnemyRemainingHealth,
        bool EnemyDefeated, int EnemyDamage, string? EnemyAbilityUsed,
        int PlayerRemainingHealth, bool PlayerDefeated, bool PlayerHardcoreDeath,
        int XpEarned = 0, int GoldEarned = 0,
        string? AbilityId = null, int AbilityDamage = 0, int HealthRestored = 0,
        int ManaCost = 0, int PlayerRemainingMana = 0);

    /// <summary>Payload for <c>CombatEnded</c> hub event.</summary>
    private sealed record CombatEndedPayload(Guid CharacterId, string Reason);

    /// <summary>Payload for <c>EnemyDefeated</c> hub event.</summary>
    private sealed record EnemyDefeatedPayload(Guid CharacterId);

    /// <summary>Payload for <c>CharacterRespawned</c> hub event.</summary>
    private sealed record CharacterRespawnedPayload(Guid CharacterId, int CurrentHealth, int CurrentMana);

    /// <summary>
    /// Payload for <c>ReceiveChatMessage</c> hub event.
    /// Matches <c>ChatMessageHubDto</c> defined in Veldrath.Server.Hubs.GameHub.
    /// </summary>
    private sealed record ChatMessageHubDto(
        Guid CharacterId, string Channel, string Sender,
        string Message, DateTimeOffset Timestamp);
}
