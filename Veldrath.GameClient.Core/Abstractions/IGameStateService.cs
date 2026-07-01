using System.ComponentModel;
using RealmEngine.Shared.Models;
using Veldrath.Contracts.Tilemap;
using Veldrath.GameClient.Core.Payloads;

namespace Veldrath.GameClient.Core.Abstractions;

// ── DTO records ─────────────────────────────────────────────────────────────

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

// ── Region / Zone state records ────────────────────────────────────────────

/// <summary>
/// Holds the current region state including tilemap data, exits, and player positions.
/// </summary>
public sealed record RegionState
{
    /// <summary>The region's unique identifier.</summary>
    public string RegionId { get; init; } = "";

    /// <summary>The region's display name.</summary>
    public string Name { get; init; } = "";

    /// <summary>A description of the region.</summary>
    public string Description { get; init; } = "";

    /// <summary>The region type (e.g. "forest", "mountain", "swamp").</summary>
    public string Type { get; init; } = "";

    /// <summary>The minimum character level recommended for this region.</summary>
    public int MinLevel { get; init; }

    /// <summary>The maximum character level for which this region provides challenge.</summary>
    public int MaxLevel { get; init; }

    /// <summary>The width of the region tilemap in tiles.</summary>
    public int TileWidth { get; init; }

    /// <summary>The height of the region tilemap in tiles.</summary>
    public int TileHeight { get; init; }

    /// <summary>The region tilemap data, or <c>null</c> if not yet loaded.</summary>
    public int[,]? Tiles { get; init; }

    /// <summary>Zone entry points on the region map, keyed by tile position.</summary>
    public Dictionary<(int X, int Y), string> Exits { get; init; } = new();

    /// <summary>Other players on the region map, keyed by character ID.</summary>
    public Dictionary<string, (int X, int Y, string Direction)> Players { get; init; } = new();
}

/// <summary>
/// Holds the current zone metadata.
/// </summary>
public sealed record ZoneState
{
    /// <summary>The zone's unique identifier.</summary>
    public string ZoneId { get; init; } = "";

    /// <summary>The zone's display name.</summary>
    public string Name { get; init; } = "";

    /// <summary>A description of the zone.</summary>
    public string Description { get; init; } = "";

    /// <summary>The zone type (e.g. "town", "wilderness", "dungeon").</summary>
    public string Type { get; init; } = "";

    /// <summary>The minimum character level recommended for this zone.</summary>
    public int MinLevel { get; init; }

    /// <summary>Whether this zone has an inn where characters can rest.</summary>
    public bool HasInn { get; init; }

    /// <summary>Whether this zone has a merchant.</summary>
    public bool HasMerchant { get; init; }
}

/// <summary>
/// Per-session game state manager.  Holds the authoritative state for the current player's
/// game session and implements <see cref="INotifyPropertyChanged"/> so consumers
/// (Blazor components, Avalonia ViewModels) can react to state changes.
/// Call <c>Apply*</c> methods from hub event handlers to update state.
/// </summary>
public interface IGameStateService : INotifyPropertyChanged
{
    // ── Connection state ────────────────────────────────────────────────────

    /// <summary>The server-assigned connection ID for the current SignalR session, or <c>null</c>.</summary>
    string? ServerConnectionId { get; }

    /// <summary>Whether the SignalR connection to the game hub is currently established.</summary>
    bool IsConnected { get; }

    // ── Character state ─────────────────────────────────────────────────────

    /// <summary>The currently selected character's identifier, or <c>null</c> if none selected.</summary>
    string? CurrentCharacterId { get; }

    /// <summary>The currently selected character's name, or <c>null</c> if none selected.</summary>
    string? CurrentCharacterName { get; }

    /// <summary>The currently selected character's level, or <c>0</c> if none selected.</summary>
    int CurrentCharacterLevel { get; }

    /// <summary>The amount of gold the current character possesses, or <c>0</c> if none selected.</summary>
    int CurrentCharacterGold { get; }

    // ── Inventory & equipment ───────────────────────────────────────────────

    /// <summary>The items currently in the character's inventory bag.</summary>
    IReadOnlyList<Item> InventoryItems { get; }

    /// <summary>The items currently equipped by the character, keyed by slot name (Head, Chest, MainHand, etc.).</summary>
    IReadOnlyDictionary<string, Item> EquippedItems { get; }

    // ── Shop state ──────────────────────────────────────────────────────────

    /// <summary>The current merchant's shop catalog, or an empty list if no merchant is open.</summary>
    IReadOnlyList<ShopItemEntry> ShopCatalog { get; }

    // ── Zone state ──────────────────────────────────────────────────────────

    /// <summary>The current zone's unique identifier, or <c>null</c> if not in a zone.</summary>
    string? CurrentZoneId { get; }

    /// <summary>The display name of the current zone, or <c>null</c> if not in a zone.</summary>
    string? CurrentZoneName { get; }

    /// <summary>The tile map for the current zone, or <c>null</c> if not yet loaded.</summary>
    object? ZoneTileMap { get; }

    // ── Region state (G48-G52) ──────────────────────────────────────────────

    /// <summary>The current region state including tilemap data, exits, and player positions.</summary>
    RegionState CurrentRegion { get; }

    /// <summary>The current zone metadata (description, type, facilities).</summary>
    ZoneState CurrentZone { get; }

    /// <summary>The slug of the current zone location (POI), or <c>null</c> if not at a named location.</summary>
    string? CurrentZoneLocationSlug { get; }

    /// <summary>The zone locations available in the current zone for navigation.</summary>
    IReadOnlyList<ZoneLocationEntry> ZoneLocations { get; }

    /// <summary>The traversal connections available from the current location.</summary>
    IReadOnlyList<ZoneConnectionLink> CurrentLocationConnections { get; }

    // ── Character info (basic) ──────────────────────────────────────────────

    /// <summary>The currently selected character's basic information, or <c>null</c> if none selected.</summary>
    CharacterBasicInfo? CurrentCharacterInfo { get; }

    // ── Chat state ───────────────────────────────────────────────────────────

    /// <summary>The ordered list of chat messages received during the current session.</summary>
    IReadOnlyList<ChatMessage> ChatMessages { get; }

    /// <summary>Raised when a new chat message is received.</summary>
    event Action<string>? ChatMessageReceived;

    /// <summary>Subscribe to any state change. Returns an <see cref="IDisposable"/> that unsubscribes.</summary>
    /// <param name="handler">The handler to invoke when any property changes.</param>
    /// <returns>An <see cref="IDisposable"/> that unsubscribes the handler when disposed.</returns>
    IDisposable OnStateChanged(Action handler);

    // ── Zone entity state ───────────────────────────────────────────────────

    /// <summary>Other players occupying the current zone.</summary>
    IReadOnlyList<OccupantInfo> ZoneOccupants { get; }

    /// <summary>Visible enemies in the current zone.</summary>
    IReadOnlyList<EnemyInfo> ZoneEnemies { get; }

    /// <summary>The player's current tile column within the zone.</summary>
    int PlayerTileX { get; }

    /// <summary>The player's current tile row within the zone.</summary>
    int PlayerTileY { get; }

    // ── Combat state ────────────────────────────────────────────────────────

    /// <summary>Whether the player is currently engaged in combat.</summary>
    bool IsInCombat { get; }

    /// <summary>The enemy the player is currently fighting, or <c>null</c> if not in combat.</summary>
    EnemyInfo? CombatEnemy { get; }

    /// <summary>The result description of the last combat action (e.g. "You hit for 12 damage").</summary>
    string? LastCombatActionResult { get; }

    // ── Apply methods (called from hub event handlers) ───────────────────────

    /// <summary>Updates state after a character has been selected on the server.</summary>
    /// <param name="payload">The character selected payload from the hub event.</param>
    void ApplyCharacterSelected(CharacterSelectedPayload payload);

    /// <summary>Updates state after entering a zone.</summary>
    /// <param name="payload">The zone entered payload from the hub event.</param>
    void ApplyZoneEntered(ZoneEnteredPayload payload);

    /// <summary>Updates state when combat starts.</summary>
    /// <param name="payload">The combat started payload from the hub event.</param>
    void ApplyCombatStarted(CombatStartedPayload payload);

    /// <summary>Updates state after a combat turn has been processed.</summary>
    /// <param name="payload">The combat turn payload from the hub event.</param>
    void ApplyCombatTurn(CombatTurnPayload payload);

    /// <summary>Updates state when combat ends.</summary>
    /// <param name="payload">The combat ended payload from the hub event.</param>
    void ApplyCombatEnded(CombatEndedPayload payload);

    /// <summary>Appends a chat message to the chat log.</summary>
    /// <param name="payload">The chat message payload from the hub event.</param>
    void ApplyChatMessage(ChatMessageHubDto payload);

    /// <summary>Updates state when another player enters the zone.</summary>
    /// <param name="payload">The player entered payload from the hub event.</param>
    void ApplyPlayerEntered(PlayerEnteredPayload payload);

    /// <summary>Updates state when another player leaves the zone.</summary>
    /// <param name="payload">The player left payload from the hub event.</param>
    void ApplyPlayerLeft(PlayerLeftPayload payload);

    /// <summary>Updates the player's position after a movement action.</summary>
    /// <param name="payload">The character moved payload from the hub event.</param>
    void ApplyCharacterMoved(Veldrath.Contracts.Tilemap.CharacterMovedPayload payload);

    /// <summary>Replaces the current zone entities snapshot (occupants and enemies).</summary>
    /// <param name="payload">The zone entities snapshot payload from the hub event.</param>
    void ApplyZoneEntitiesSnapshot(Veldrath.Contracts.Tilemap.ZoneEntitiesSnapshotPayload payload);

    /// <summary>Handles notification that an enemy has been defeated.</summary>
    /// <param name="payload">The enemy defeated payload from the hub event.</param>
    void ApplyEnemyDefeated(EnemyDefeatedPayload payload);

    // ── Inventory & equipment Apply methods ─────────────────────────────────

    /// <summary>Replaces the entire inventory and equipped items with a fresh snapshot from the server.</summary>
    /// <param name="items">The items in the character's inventory bag.</param>
    /// <param name="equipped">The items currently equipped, keyed by slot name.</param>
    void ApplyInventoryUpdated(IReadOnlyList<Item> items, IReadOnlyDictionary<string, Item> equipped);

    /// <summary>Updates a single equipment slot after an equip or unequip action.</summary>
    /// <param name="slot">The equipment slot name (Head, Chest, MainHand, etc.).</param>
    /// <param name="item">The item now in the slot, or <c>null</c> if the slot was unequipped.</param>
    void ApplyEquipmentChanged(string slot, Item? item);

    // ── Shop Apply methods ──────────────────────────────────────────────────

    /// <summary>Replaces the current shop catalog with a fresh list from the server.</summary>
    /// <param name="catalog">The items for sale in the current merchant's shop.</param>
    void ApplyShopCatalogUpdated(IReadOnlyList<ShopItemEntry> catalog);

    // ── Quest log ───────────────────────────────────────────────────────────

    /// <summary>Gets the list of currently active quests for the selected character.</summary>
    IReadOnlyList<QuestLogEntry> QuestLog { get; }

    /// <summary>Gets the list of completed quests for the selected character.</summary>
    IReadOnlyList<QuestLogEntry> CompletedQuests { get; }

    /// <summary>Replaces the quest log with a fresh snapshot from the server.</summary>
    /// <param name="active">The currently active quests.</param>
    /// <param name="completed">The completed quests.</param>
    void ApplyQuestLogUpdated(IReadOnlyList<QuestLogEntry> active, IReadOnlyList<QuestLogEntry> completed);

    // ── Settings ────────────────────────────────────────────────────────────

    /// <summary>Gets the current game settings for this session.</summary>
    GameSettingsState Settings { get; }

    /// <summary>Updates the game settings with the provided values.</summary>
    /// <param name="settings">The new settings to apply.</param>
    void ApplySettings(GameSettingsState settings);

    // ── Live character state ────────────────────────────────────────────────

    /// <summary>Gets the current character's live state snapshot for combat and progression.</summary>
    CharacterState CurrentCharacter { get; }

    // ── Kick state ──────────────────────────────────────────────────────────

    /// <summary>Whether the player has been forcibly kicked from the server.</summary>
    bool IsKicked { get; }

    /// <summary>The reason for the kick, or <c>null</c> if not kicked.</summary>
    string? KickReason { get; }

    /// <summary>Applies a kick event, setting the kick reason and flag.</summary>
    /// <param name="reason">The human-readable reason for the kick.</param>
    void ApplyKicked(string reason);

    // ── Progression Apply methods ───────────────────────────────────────────

    /// <summary>Updates character state after gaining experience.</summary>
    /// <param name="newLevel">The character's new level.</param>
    /// <param name="newXP">The character's total accumulated XP.</param>
    /// <param name="newLeveledUpTo">The level reached if a level-up occurred, or 0.</param>
    void ApplyExperienceGained(int newLevel, int newXP, int newLeveledUpTo);

    /// <summary>Updates character state after gold changes.</summary>
    /// <param name="goldAdded">The amount of gold added (positive) or spent (negative).</param>
    /// <param name="newGoldTotal">The character's total gold after the transaction.</param>
    void ApplyGoldChanged(int goldAdded, int newGoldTotal);

    /// <summary>Updates character state after taking damage.</summary>
    /// <param name="damage">The amount of damage taken.</param>
    /// <param name="currentHP">The character's current health after taking damage.</param>
    /// <param name="maxHP">The character's maximum health.</param>
    /// <param name="isDead">Whether the character died from this damage.</param>
    void ApplyDamageTaken(int damage, int currentHP, int maxHP, bool isDead);

    // ── Shop state ──────────────────────────────────────────────────────────

    /// <summary>Whether the character is currently in a shop or merchant interface.</summary>
    bool InShop { get; }

    /// <summary>The display name of the current shop, or <c>null</c> if not in a shop.</summary>
    string? ShopName { get; }

    /// <summary>Updates state after visiting a shop or merchant.</summary>
    /// <param name="zoneId">The zone identifier where the shop is located.</param>
    /// <param name="zoneName">The display name of the shop or zone.</param>
    void ApplyShopVisited(string zoneId, string zoneName);

    // ── Inventory transaction Apply methods ─────────────────────────────────

    /// <summary>Applies an inventory transaction (buy, sell, drop) to the character's state.</summary>
    /// <param name="itemName">The display name of the transacted item.</param>
    /// <param name="newGold">The character's new gold total after the transaction.</param>
    /// <param name="inventory">The full inventory snapshot after the transaction.</param>
    void ApplyItemTransacted(string itemName, int newGold, List<Item> inventory);

    // ── Attribute allocation ────────────────────────────────────────────────

    /// <summary>Updates character state after attribute points are allocated.</summary>
    /// <param name="remaining">The number of unspent attribute points remaining.</param>
    /// <param name="stats">The current attribute values keyed by attribute name (Strength, Dexterity, etc.).</param>
    void ApplyAttributePointsAllocated(int remaining, Dictionary<string, int> stats);

    // ── Rest ────────────────────────────────────────────────────────────────

    /// <summary>Updates character state after resting.</summary>
    /// <param name="hp">The character's health after resting.</param>
    /// <param name="maxHp">The character's maximum health.</param>
    /// <param name="mp">The character's mana after resting.</param>
    /// <param name="maxMp">The character's maximum mana.</param>
    /// <param name="gold">The character's gold after resting (may be deducted for inn costs).</param>
    void ApplyCharacterRested(int hp, int maxHp, int mp, int maxMp, int gold);

    // ── Ability ─────────────────────────────────────────────────────────────

    /// <summary>Updates character state after using an ability.</summary>
    /// <param name="abilityId">The ability identifier slug.</param>
    /// <param name="remainingMana">The character's mana after using the ability.</param>
    /// <param name="hpRestored">The amount of health restored by the ability, if any.</param>
    void ApplyAbilityUsed(string abilityId, int remainingMana, int hpRestored);

    // ── Skills ──────────────────────────────────────────────────────────────

    /// <summary>Updates character state after gaining skill experience.</summary>
    /// <param name="skillId">The skill identifier slug.</param>
    /// <param name="xpGained">The amount of skill XP gained.</param>
    /// <param name="newRank">The skill's new rank after the XP gain.</param>
    /// <param name="rankedUp">Whether the skill gained a rank from this XP gain.</param>
    void ApplySkillXpGained(string skillId, int xpGained, int newRank, bool rankedUp);

    // ── Crafting ────────────────────────────────────────────────────────────

    /// <summary>Updates character state after successfully crafting an item.</summary>
    /// <param name="recipeName">The display name of the crafted recipe.</param>
    /// <param name="goldSpent">The amount of gold spent on crafting.</param>
    /// <param name="remainingGold">The character's gold after crafting.</param>
    void ApplyItemCrafted(string recipeName, int goldSpent, int remainingGold);

    // ── Dungeon ─────────────────────────────────────────────────────────────

    /// <summary>The current dungeon identifier, or <c>null</c> if not in a dungeon.</summary>
    string? CurrentDungeonId { get; }

    /// <summary>Updates state after entering a dungeon.</summary>
    /// <param name="dungeonId">The dungeon's unique identifier.</param>
    void ApplyDungeonEntered(string dungeonId);

    // ── Zone / Region / Location Apply methods (G36-G47) ────────────────────

    /// <summary>Updates state after exiting a zone into the region map.</summary>
    /// <param name="regionId">The region the player is exiting into.</param>
    /// <param name="tileX">The tile column of the exit position on the region map.</param>
    /// <param name="tileY">The tile row of the exit position on the region map.</param>
    void ApplyZoneExited(string regionId, int tileX, int tileY);

    /// <summary>Stores the region map data received from the server.</summary>
    /// <param name="region">The region state containing tilemap and exit data.</param>
    void ApplyRegionMapReceived(RegionState region);

    /// <summary>Updates state when the player's current region changes.</summary>
    /// <param name="regionId">The new region identifier.</param>
    /// <param name="tileX">The tile column of the entry position.</param>
    /// <param name="tileY">The tile row of the entry position.</param>
    void ApplyRegionChanged(string regionId, int tileX, int tileY);

    /// <summary>Updates another player's position on the region map.</summary>
    /// <param name="charId">The character identifier.</param>
    /// <param name="x">The tile column of the player's new position.</param>
    /// <param name="y">The tile row of the player's new position.</param>
    /// <param name="direction">The facing direction of the player.</param>
    void ApplyRegionPlayerMoved(string charId, int x, int y, string direction);

    /// <summary>Updates state after entering a named location within a zone.</summary>
    /// <param name="location">The location entered payload containing enemies and connections.</param>
    void ApplyLocationEntered(LocationEnteredPayload location);

    /// <summary>Updates state when a new zone location is discovered or unlocked.</summary>
    /// <param name="slug">The location identifier slug.</param>
    /// <param name="name">The location display name.</param>
    /// <param name="type">The location type.</param>
    void ApplyZoneLocationUnlocked(string slug, string name, string type);

    /// <summary>Updates state after traversing a connection between locations or zones.</summary>
    /// <param name="slug">The connection identifier slug.</param>
    /// <param name="zoneId">The zone identifier after traversal.</param>
    /// <param name="isCrossZone">Whether this traversal crossed zone boundaries.</param>
    /// <param name="connections">The connections available at the new location.</param>
    void ApplyConnectionTraversed(string slug, string zoneId, bool isCrossZone, IReadOnlyList<ZoneConnectionLink> connections);

    // ── System / infrastructure Apply methods ───────────────────────────────

    /// <summary>Appends a system message to the chat log.</summary>
    /// <param name="message">The system message text.</param>
    void ApplySystemMessage(string message);

    /// <summary>Updates the connection ID after a successful hub connection.</summary>
    /// <param name="connectionId">The server-assigned connection ID.</param>
    void ApplyServerInfo(string connectionId);

    /// <summary>Updates the zone tile map (e.g. after requesting a fresh map).</summary>
    /// <param name="tileMap">The tile map data.</param>
    void ApplyZoneTileMap(Tile[,] tileMap);

    // ── Respawn ─────────────────────────────────────────────────────────────

    /// <summary>Restores the character after respawn: clears death flag, restores HP/MP to max.</summary>
    void ApplyCharacterRespawned();

    // ── Lifecycle ───────────────────────────────────────────────────────────

    /// <summary>Resets all state to defaults for logout or session end.</summary>
    void Reset();
}

/// <summary>
/// Represents a single quest entry in the character's quest journal.
/// </summary>
/// <param name="Id">The unique quest identifier.</param>
/// <param name="Title">The display title of the quest.</param>
/// <param name="Description">The flavor text or objective description.</param>
/// <param name="Progress">The current progress count toward completion.</param>
/// <param name="Total">The total required progress for completion.</param>
/// <param name="XpReward">Experience points awarded on completion.</param>
/// <param name="GoldReward">Gold awarded on completion.</param>
/// <param name="Objectives">Objective names and their required counts.</param>
/// <param name="ProgressData">Current progress toward each objective, keyed by objective name.</param>
public sealed record QuestLogEntry(
    string Id,
    string Title,
    string Description,
    int Progress,
    int Total,
    int XpReward,
    int GoldReward,
    IReadOnlyDictionary<string, int> Objectives,
    IReadOnlyDictionary<string, int> ProgressData)
{
    /// <summary>Gets whether the quest is complete (progress has reached the total).</summary>
    public bool IsComplete => Progress >= Total;
}

/// <summary>
/// Holds the current game settings for a player session.
/// </summary>
/// <param name="MusicVolume">Music volume percentage (0-100). Default is 80.</param>
/// <param name="SfxVolume">SFX volume percentage (0-100). Default is 80.</param>
/// <param name="IsMuted">Whether all audio is muted. Default is <c>false</c>.</param>
/// <param name="UiScale">UI scale percentage. Default is 100.</param>
/// <param name="Theme">The UI theme name (dark, light, sepia). Default is "dark".</param>
/// <param name="FontSize">The font size category (small, medium, large, x-large). Default is "medium".</param>
/// <param name="ReducedMotion">Whether reduced motion is enabled for accessibility. Default is <c>false</c>.</param>
public sealed record GameSettingsState(
    int MusicVolume = 80,
    int SfxVolume = 80,
    bool IsMuted = false,
    int UiScale = 100,
    string Theme = "dark",
    string FontSize = "medium",
    bool ReducedMotion = false);

/// <summary>
/// Represents a single item for sale in a merchant's shop catalog.
/// </summary>
/// <param name="Slug">The URL-safe item identifier.</param>
/// <param name="Name">The display name of the item.</param>
/// <param name="Rarity">The <see cref="ItemRarity"/> tier of the item.</param>
/// <param name="Price">The purchase price in gold.</param>
public readonly record struct ShopItemEntry(string Slug, string Name, ItemRarity Rarity, int Price);

/// <summary>
/// Holds the live character state for combat and progression tracking.
/// Instances are immutable; updates create new instances via <c>with</c> expressions.
/// </summary>
public sealed record CharacterState
{
    /// <summary>The character's current level.</summary>
    public int Level { get; init; }

    /// <summary>The character's total accumulated experience points.</summary>
    public int XP { get; init; }

    /// <summary>The amount of gold the character possesses.</summary>
    public int Gold { get; init; }

    /// <summary>The character's current health points.</summary>
    public int CurrentHealth { get; init; }

    /// <summary>The character's maximum health points.</summary>
    public int MaxHealth { get; init; }

    /// <summary>The character's current mana points.</summary>
    public int CurrentMana { get; init; }

    /// <summary>The character's maximum mana points.</summary>
    public int MaxMana { get; init; }

    /// <summary>Whether the character is currently dead.</summary>
    public bool IsDead { get; init; }

    /// <summary>The character's Strength attribute value.</summary>
    public int Strength { get; init; }

    /// <summary>The character's Dexterity attribute value.</summary>
    public int Dexterity { get; init; }

    /// <summary>The character's Constitution attribute value.</summary>
    public int Constitution { get; init; }

    /// <summary>The character's Intelligence attribute value.</summary>
    public int Intelligence { get; init; }

    /// <summary>The character's Wisdom attribute value.</summary>
    public int Wisdom { get; init; }

    /// <summary>The character's Charisma attribute value.</summary>
    public int Charisma { get; init; }

    /// <summary>The number of unspent attribute points available for allocation.</summary>
    public int UnspentAttributePoints { get; init; }
}
