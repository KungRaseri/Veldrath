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
}

/// <summary>
/// Represents a single item for sale in a merchant's shop catalog.
/// </summary>
/// <param name="Slug">The URL-safe item identifier.</param>
/// <param name="Name">The display name of the item.</param>
/// <param name="Rarity">The <see cref="ItemRarity"/> tier of the item.</param>
/// <param name="Price">The purchase price in gold.</param>
public readonly record struct ShopItemEntry(string Slug, string Name, ItemRarity Rarity, int Price);
