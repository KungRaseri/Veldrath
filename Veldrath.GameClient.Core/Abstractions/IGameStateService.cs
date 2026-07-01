using System.ComponentModel;
using RealmEngine.Shared.Models;
using Veldrath.Contracts.Tilemap;
using Veldrath.GameClient.Core.Payloads;

namespace Veldrath.GameClient.Core.Abstractions;

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

    // ── Combat state ────────────────────────────────────────────────────────

    /// <summary>Whether the player is currently engaged in combat.</summary>
    bool IsInCombat { get; }

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
