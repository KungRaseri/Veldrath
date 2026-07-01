using System.ComponentModel;
using System.Runtime.CompilerServices;
using RealmEngine.Shared.Models;
using Veldrath.GameClient.Core.Abstractions;
using Veldrath.GameClient.Core.Payloads;

namespace Veldrath.GameClient.Core.Services;

// ── DTO records used by GameStateService ─────────────────────────────────────

/// <summary>Basic character information displayed on the character select screen and used in-game.</summary>
/// <param name="Id">The unique character identifier.</param>
/// <param name="Name">The character's display name.</param>
/// <param name="ClassName">The character's class (e.g. "Warrior", "Mage").</param>
/// <param name="Level">The character's current level.</param>
/// <param name="Experience">Total experience points earned.</param>
/// <param name="CurrentHealth">Current health points.</param>
/// <param name="MaxHealth">Maximum health points.</param>
/// <param name="CurrentMana">Current mana points.</param>
/// <param name="MaxMana">Maximum mana points.</param>
/// <param name="Gold">Gold coins in the character's possession.</param>
/// <param name="ExperienceToNextLevel">Experience points needed to reach the next level. Defaults to a calculation when not provided.</param>
public record CharacterBasicInfo(
    Guid Id,
    string Name,
    string ClassName,
    int Level,
    long Experience,
    int CurrentHealth,
    int MaxHealth,
    int CurrentMana,
    int MaxMana,
    int Gold,
    long ExperienceToNextLevel = 0)
{
    /// <summary>Gets the experience points needed to reach the next level, or a calculated default when not provided.</summary>
    public long EffectiveExperienceToNextLevel => ExperienceToNextLevel > 0 ? ExperienceToNextLevel : Level * 1000L;
}

/// <summary>A single tile on the zone tilemap.  The <see cref="Type"/> value maps to a CSS class for rendering.</summary>
/// <param name="X">Tile column.</param>
/// <param name="Y">Tile row.</param>
/// <param name="Type">Tile type identifier (0=grass, 1=wall, 2=water, 3=door, 4=path, 5=dirt, -1=void).</param>
/// <param name="IsBlocked">Whether the tile blocks movement.</param>
public record Tile(int X, int Y, int Type, bool IsBlocked);

/// <summary>A chat message received from the game server.</summary>
/// <param name="CharacterId">The sending character's unique identifier.</param>
/// <param name="Channel">The chat channel (e.g. "zone", "global", "whisper").</param>
/// <param name="Sender">The display name of the sender.</param>
/// <param name="Message">The message text.</param>
/// <param name="Timestamp">When the message was sent.</param>
public record ChatMessage(
    Guid CharacterId,
    string Channel,
    string Sender,
    string Message,
    DateTimeOffset Timestamp);

/// <summary>Information about another player character occupying the same zone.</summary>
/// <param name="CharacterId">The occupying character's unique identifier.</param>
/// <param name="CharacterName">The occupying character's display name.</param>
/// <param name="EnteredAt">When this character entered the zone.</param>
/// <param name="TileX">The occupant's tile column, or <c>-1</c> if unknown.</param>
/// <param name="TileY">The occupant's tile row, or <c>-1</c> if unknown.</param>
public record OccupantInfo(Guid CharacterId, string CharacterName, DateTimeOffset EnteredAt, int TileX = -1, int TileY = -1);

/// <summary>Information about an enemy in the current zone.</summary>
/// <param name="EnemyId">The unique enemy identifier.</param>
/// <param name="EnemyName">The display name of the enemy.</param>
/// <param name="Level">The enemy's level.</param>
/// <param name="CurrentHealth">The enemy's current health points.</param>
/// <param name="MaxHealth">The enemy's maximum health points.</param>
/// <param name="TileX">The column of the tile the enemy is standing on.</param>
/// <param name="TileY">The row of the tile the enemy is standing on.</param>
public record EnemyInfo(
    Guid EnemyId,
    string EnemyName,
    int Level,
    int CurrentHealth,
    int MaxHealth,
    int TileX,
    int TileY);

// ── GameStateService ─────────────────────────────────────────────────────────

/// <summary>
/// Per-circuit game state manager.  Holds the authoritative state for the current player's
/// game session and implements <see cref="INotifyPropertyChanged"/> so consumers
/// can react to state changes.  Call <c>Apply*</c> methods from hub event handlers to
/// update state.
/// Implements <see cref="IGameStateService"/> for abstraction across consumers.
/// </summary>
public sealed class GameStateService : IGameStateService
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raised when a new chat message is received.  Components can subscribe to this
    /// for targeted updates without re-rendering on every <see cref="PropertyChanged"/> event.
    /// </summary>
    public event Action<string>? ChatMessageReceived;

    /// <summary>
    /// Raised when a new system message is received.  Components can subscribe to this
    /// for targeted updates without re-rendering on every <see cref="PropertyChanged"/> event.
    /// </summary>
    public event Action<string>? SystemMessageReceived;

    // ── Connection state ────────────────────────────────────────────────────────

    /// <summary>The server-assigned connection ID for the current SignalR session.</summary>
    public string? ServerConnectionId { get; private set; }

    /// <summary>Whether the SignalR connection to the game hub is currently established.</summary>
    public bool IsConnected { get; private set; }

    // ── Character state ─────────────────────────────────────────────────────────

    /// <summary>The currently selected character's basic information, or <c>null</c> if none selected.</summary>
    public CharacterBasicInfo? CurrentCharacter { get; private set; }

    /// <inheritdoc />
    string? IGameStateService.CurrentCharacterId => CurrentCharacter?.Id.ToString();

    /// <inheritdoc />
    string? IGameStateService.CurrentCharacterName => CurrentCharacter?.Name;

    /// <inheritdoc />
    int IGameStateService.CurrentCharacterLevel => CurrentCharacter?.Level ?? 0;

    /// <inheritdoc />
    int IGameStateService.CurrentCharacterGold => CurrentCharacter?.Gold ?? 0;

    /// <inheritdoc />
    object? IGameStateService.ZoneTileMap => ZoneTileMap;

    // ── Inventory & equipment ───────────────────────────────────────────────────

    /// <inheritdoc />
    IReadOnlyList<Item> IGameStateService.InventoryItems => InventoryItems;

    /// <inheritdoc />
    IReadOnlyDictionary<string, Item> IGameStateService.EquippedItems => EquippedItems;

    /// <summary>The items currently in the character's inventory bag.</summary>
    public List<Item> InventoryItems { get; private set; } = [];

    /// <summary>The items currently equipped by the character, keyed by slot name.</summary>
    public Dictionary<string, Item> EquippedItems { get; private set; } = [];

    // ── Shop state ──────────────────────────────────────────────────────────────

    /// <inheritdoc />
    IReadOnlyList<ShopItemEntry> IGameStateService.ShopCatalog => ShopCatalog;

    /// <summary>The current merchant's shop catalog.</summary>
    public List<ShopItemEntry> ShopCatalog { get; private set; } = [];

    // ── Zone state ──────────────────────────────────────────────────────────────

    /// <summary>The current zone's unique identifier.</summary>
    public string? CurrentZoneId { get; private set; }

    /// <summary>The display name of the current zone.</summary>
    public string? CurrentZoneName { get; private set; }

    /// <summary>The tile map for the current zone, or <c>null</c> if not yet loaded.</summary>
    public Tile[,]? ZoneTileMap { get; private set; }

    /// <summary>The player's current tile column within the zone.</summary>
    public int PlayerTileX { get; private set; }

    /// <summary>The player's current tile row within the zone.</summary>
    public int PlayerTileY { get; private set; }

    /// <summary>Other players occupying the current zone.</summary>
    public List<OccupantInfo> ZoneOccupants { get; private set; } = [];

    /// <summary>Visible enemies in the current zone.</summary>
    public List<EnemyInfo> ZoneEnemies { get; private set; } = [];

    // ── Combat state ────────────────────────────────────────────────────────────

    /// <summary>Whether the player is currently engaged in combat.</summary>
    public bool IsInCombat { get; private set; }

    /// <summary>The enemy the player is currently fighting, or <c>null</c> if not in combat.</summary>
    public EnemyInfo? CombatEnemy { get; private set; }

    /// <summary>The result description of the last combat action (e.g. "You hit for 12 damage").</summary>
    public string? LastCombatActionResult { get; private set; }

    // ── Chat state ──────────────────────────────────────────────────────────────

    /// <summary>The ordered list of chat messages received during the current session.</summary>
    public List<ChatMessage> ChatMessages { get; private set; } = [];

    // ── IGameStateService Apply methods (called from hub event handlers) ────────

    /// <inheritdoc />
    public void ApplyCharacterSelected(CharacterSelectedPayload payload)
    {
        var character = new CharacterBasicInfo(
            payload.Id, payload.Name, payload.ClassName, payload.Level,
            payload.Experience, payload.CurrentHealth, payload.MaxHealth,
            payload.CurrentMana, payload.MaxMana, payload.Gold);

        ApplyCharacterSelected(character, payload.CurrentZoneId);
    }

    /// <inheritdoc />
    public void ApplyZoneEntered(ZoneEnteredPayload payload)
    {
        ApplyZoneEntered(payload.Id, payload.Name, 0, 0, null);
    }

    /// <inheritdoc />
    public void ApplyCombatStarted(CombatStartedPayload payload)
    {
        var enemy = new EnemyInfo(
            payload.EnemyId, payload.EnemyName, payload.EnemyLevel,
            payload.EnemyCurrentHealth, payload.EnemyMaxHealth, 0, 0);
        ApplyCombatStarted(enemy);
    }

    /// <inheritdoc />
    public void ApplyCombatTurn(CombatTurnPayload payload)
    {
        var resultText = BuildCombatResultText(payload);
        ApplyCombatTurn(resultText);
        ApplyCharacterHealth(payload.PlayerRemainingHealth);
    }

    /// <inheritdoc />
    public void ApplyCombatEnded(CombatEndedPayload payload)
    {
        ApplyCombatEnded();
        ApplySystemMessage($"Combat ended: {payload.Reason}");
    }

    /// <inheritdoc />
    public void ApplyChatMessage(ChatMessageHubDto payload)
    {
        var msg = new ChatMessage(
            payload.CharacterId, payload.Channel, payload.Sender,
            payload.Message, payload.Timestamp);
        ApplyChatMessage(msg);
    }

    /// <inheritdoc />
    public void ApplyPlayerEntered(PlayerEnteredPayload payload)
    {
        var occupant = new OccupantInfo(payload.CharacterId, payload.CharacterName, DateTimeOffset.UtcNow);
        ApplyPlayerEntered(occupant);
    }

    /// <inheritdoc />
    public void ApplyPlayerLeft(PlayerLeftPayload payload)
    {
        ApplyPlayerLeft(payload.CharacterId);
    }

    /// <inheritdoc />
    public void ApplyCharacterMoved(Veldrath.Contracts.Tilemap.CharacterMovedPayload payload)
    {
        ApplyCharacterMoved(payload.TileX, payload.TileY);
    }

    /// <inheritdoc />
    public void ApplyZoneEntitiesSnapshot(Veldrath.Contracts.Tilemap.ZoneEntitiesSnapshotPayload payload)
    {
        ApplySystemMessage("Zone entities snapshot received.");
    }

    /// <inheritdoc />
    public void ApplyEnemyDefeated(EnemyDefeatedPayload payload)
    {
        ApplySystemMessage("An enemy has been defeated!");
    }

    // ── Inventory & equipment Apply methods ─────────────────────────────────────

    /// <inheritdoc />
    public void ApplyInventoryUpdated(IReadOnlyList<Item> items, IReadOnlyDictionary<string, Item> equipped)
    {
        InventoryItems = [.. items];
        EquippedItems = new Dictionary<string, Item>(equipped);
        RaisePropertyChanged(nameof(InventoryItems));
        RaisePropertyChanged(nameof(EquippedItems));
    }

    /// <inheritdoc />
    public void ApplyEquipmentChanged(string slot, Item? item)
    {
        if (item is null)
            EquippedItems.Remove(slot);
        else
            EquippedItems[slot] = item;
        RaisePropertyChanged(nameof(EquippedItems));
    }

    // ── Shop Apply methods ──────────────────────────────────────────────────────

    /// <inheritdoc />
    public void ApplyShopCatalogUpdated(IReadOnlyList<ShopItemEntry> catalog)
    {
        ShopCatalog = [.. catalog];
        RaisePropertyChanged(nameof(ShopCatalog));
    }

    // ── Existing Apply methods (preserved for backward compatibility) ─────────

    /// <summary>Updates the connection ID after a successful hub connection.</summary>
    /// <param name="connectionId">The server-assigned connection ID.</param>
    public void ApplyServerInfo(string connectionId)
    {
        ServerConnectionId = connectionId;
        IsConnected = true;
        RaisePropertyChanged(nameof(ServerConnectionId));
        RaisePropertyChanged(nameof(IsConnected));
    }

    /// <summary>Updates state after a character has been selected.</summary>
    /// <param name="character">The selected character's basic information.</param>
    /// <param name="currentZoneId">Optional zone ID the character is currently in (from server).</param>
    public void ApplyCharacterSelected(CharacterBasicInfo character, string? currentZoneId = null)
    {
        CurrentCharacter = character;
        if (currentZoneId is not null)
            CurrentZoneId = currentZoneId;
        PlayerTileX = 0;
        PlayerTileY = 0;
        RaisePropertyChanged(nameof(CurrentCharacter));
        RaisePropertyChanged(nameof(CurrentZoneId));
        RaisePropertyChanged(nameof(PlayerTileX));
        RaisePropertyChanged(nameof(PlayerTileY));
    }

    /// <summary>Updates state after entering a zone.</summary>
    /// <param name="zoneId">The zone identifier.</param>
    /// <param name="zoneName">The zone display name.</param>
    /// <param name="tileX">The player's spawn tile column.</param>
    /// <param name="tileY">The player's spawn tile row.</param>
    /// <param name="tileMap">The zone tile map, or <c>null</c> if not yet loaded.</param>
    public void ApplyZoneEntered(string zoneId, string zoneName, int tileX, int tileY, Tile[,]? tileMap)
    {
        CurrentZoneId = zoneId;
        CurrentZoneName = zoneName;
        PlayerTileX = tileX;
        PlayerTileY = tileY;
        ZoneTileMap = tileMap;
        RaisePropertyChanged(nameof(CurrentZoneId));
        RaisePropertyChanged(nameof(CurrentZoneName));
        RaisePropertyChanged(nameof(PlayerTileX));
        RaisePropertyChanged(nameof(PlayerTileY));
        RaisePropertyChanged(nameof(ZoneTileMap));
    }

    /// <summary>Updates the zone tile map (e.g. after requesting a fresh map).</summary>
    /// <param name="tileMap">The tile map data.</param>
    public void ApplyZoneTileMap(Tile[,] tileMap)
    {
        ZoneTileMap = tileMap;
        RaisePropertyChanged(nameof(ZoneTileMap));
    }

    /// <summary>Updates the player's position after a movement action.</summary>
    /// <param name="tileX">The new tile column.</param>
    /// <param name="tileY">The new tile row.</param>
    public void ApplyCharacterMoved(int tileX, int tileY)
    {
        PlayerTileX = tileX;
        PlayerTileY = tileY;
        RaisePropertyChanged(nameof(PlayerTileX));
        RaisePropertyChanged(nameof(PlayerTileY));
    }

    /// <summary>Adds an occupant that entered the zone.</summary>
    /// <param name="occupant">The occupant information.</param>
    public void ApplyPlayerEntered(OccupantInfo occupant)
    {
        ZoneOccupants.Add(occupant);
        RaisePropertyChanged(nameof(ZoneOccupants));
    }

    /// <summary>Adds or updates an occupant with a known tile position.</summary>
    /// <param name="characterId">The occupant's character identifier.</param>
    /// <param name="characterName">The occupant's display name.</param>
    /// <param name="tileX">The occupant's tile column.</param>
    /// <param name="tileY">The occupant's tile row.</param>
    public void ApplyPlayerPositioned(Guid characterId, string characterName, int tileX, int tileY)
    {
        var existing = ZoneOccupants.FirstOrDefault(o => o.CharacterId == characterId);
        if (existing is not null)
        {
            var idx = ZoneOccupants.IndexOf(existing);
            ZoneOccupants[idx] = existing with { TileX = tileX, TileY = tileY };
        }
        else
        {
            ZoneOccupants.Add(new OccupantInfo(characterId, characterName, DateTimeOffset.UtcNow, tileX, tileY));
        }
        RaisePropertyChanged(nameof(ZoneOccupants));
    }

    /// <summary>Removes an occupant that left the zone.</summary>
    /// <param name="characterId">The character identifier of the leaving player.</param>
    public void ApplyPlayerLeft(Guid characterId)
    {
        ZoneOccupants.RemoveAll(o => o.CharacterId == characterId);
        RaisePropertyChanged(nameof(ZoneOccupants));
    }

    /// <summary>Updates state when combat starts with an enemy.</summary>
    /// <param name="enemy">The enemy being engaged.</param>
    public void ApplyCombatStarted(EnemyInfo enemy)
    {
        IsInCombat = true;
        CombatEnemy = enemy;
        LastCombatActionResult = null;
        RaisePropertyChanged(nameof(IsInCombat));
        RaisePropertyChanged(nameof(CombatEnemy));
        RaisePropertyChanged(nameof(LastCombatActionResult));
    }

    /// <summary>Updates state after a combat turn has been processed.</summary>
    /// <param name="result">A description of the turn result (e.g. "You hit for 12 damage").</param>
    public void ApplyCombatTurn(string result)
    {
        LastCombatActionResult = result;
        RaisePropertyChanged(nameof(LastCombatActionResult));
    }

    /// <summary>Updates state when combat ends (flee, victory, or defeat).</summary>
    public void ApplyCombatEnded()
    {
        IsInCombat = false;
        CombatEnemy = null;
        LastCombatActionResult = null;
        RaisePropertyChanged(nameof(IsInCombat));
        RaisePropertyChanged(nameof(CombatEnemy));
        RaisePropertyChanged(nameof(LastCombatActionResult));
    }

    /// <summary>Updates state when an enemy is defeated (outside of <see cref="ApplyCombatEnded"/>).</summary>
    /// <param name="enemyName">The name of the defeated enemy.</param>
    /// <param name="xpGained">The experience points gained.</param>
    public void ApplyEnemyDefeated(string enemyName, int xpGained)
    {
        var msg = $"Defeated {enemyName}! Gained {xpGained} XP.";
        ApplySystemMessage(msg);
    }

    /// <summary>Appends a chat message to the chat log.</summary>
    /// <param name="msg">The chat message to append.</param>
    public void ApplyChatMessage(ChatMessage msg)
    {
        ChatMessages.Add(msg);
        ChatMessageReceived?.Invoke(msg.Message);
        RaisePropertyChanged(nameof(ChatMessages));
    }

    /// <summary>Appends a system message to the chat log.</summary>
    /// <param name="message">The system message text.</param>
    public void ApplySystemMessage(string message)
    {
        ChatMessages.Add(new ChatMessage(
            Guid.Empty,
            "system",
            "System",
            message,
            DateTimeOffset.UtcNow));
        SystemMessageReceived?.Invoke(message);
        RaisePropertyChanged(nameof(ChatMessages));
    }

    /// <summary>Replaces the current zone occupants and enemies with a fresh snapshot.</summary>
    /// <param name="occupants">The current occupants in the zone.</param>
    /// <param name="enemies">The current enemies in the zone.</param>
    public void ApplyZoneEntitiesSnapshot(List<OccupantInfo> occupants, List<EnemyInfo> enemies)
    {
        ZoneOccupants = occupants;
        ZoneEnemies = enemies;
        RaisePropertyChanged(nameof(ZoneOccupants));
        RaisePropertyChanged(nameof(ZoneEnemies));
    }

    /// <summary>
    /// Resets all state to defaults.  Call when disconnecting, logging out, or when
    /// the game session ends.
    /// </summary>
    public void Reset()
    {
        ServerConnectionId = null;
        IsConnected = false;
        CurrentCharacter = null;
        CurrentZoneId = null;
        CurrentZoneName = null;
        ZoneTileMap = null;
        PlayerTileX = 0;
        PlayerTileY = 0;
        ZoneOccupants = [];
        ZoneEnemies = [];
        IsInCombat = false;
        CombatEnemy = null;
        LastCombatActionResult = null;
        ChatMessages = [];
        InventoryItems = [];
        EquippedItems = [];
        ShopCatalog = [];

        RaisePropertyChanged(nameof(ServerConnectionId));
        RaisePropertyChanged(nameof(IsConnected));
        RaisePropertyChanged(nameof(CurrentCharacter));
        RaisePropertyChanged(nameof(CurrentZoneId));
        RaisePropertyChanged(nameof(CurrentZoneName));
        RaisePropertyChanged(nameof(ZoneTileMap));
        RaisePropertyChanged(nameof(PlayerTileX));
        RaisePropertyChanged(nameof(PlayerTileY));
        RaisePropertyChanged(nameof(ZoneOccupants));
        RaisePropertyChanged(nameof(ZoneEnemies));
        RaisePropertyChanged(nameof(IsInCombat));
        RaisePropertyChanged(nameof(CombatEnemy));
        RaisePropertyChanged(nameof(LastCombatActionResult));
        RaisePropertyChanged(nameof(ChatMessages));
        RaisePropertyChanged(nameof(InventoryItems));
        RaisePropertyChanged(nameof(EquippedItems));
        RaisePropertyChanged(nameof(ShopCatalog));
    }

    /// <summary>Subscribe to any property change. Returns an <see cref="IDisposable"/> that unsubscribes.</summary>
    /// <param name="handler">The handler to invoke when any property changes.</param>
    /// <returns>An <see cref="IDisposable"/> that unsubscribes the handler when disposed.</returns>
    public IDisposable OnStateChanged(Action handler)
    {
        PropertyChanged += onChanged;
        return new Subscription(() => PropertyChanged -= onChanged);

        void onChanged(object? s, PropertyChangedEventArgs e) => handler();
    }

    /// <summary>Simple disposable that runs an action on dispose.</summary>
    private sealed class Subscription(Action onDispose) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose() => onDispose();
    }

    private void ApplyCharacterHealth(int playerRemainingHealth)
    {
        if (CurrentCharacter is not null)
        {
            CurrentCharacter = CurrentCharacter with { CurrentHealth = playerRemainingHealth };
            RaisePropertyChanged(nameof(CurrentCharacter));
        }
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

    private void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
