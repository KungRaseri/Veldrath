using RealmEngine.Shared.Models;
using Veldrath.GameClient.Core.Abstractions;

namespace Veldrath.GameClient.Core.Payloads;

/// <summary>
/// Hub event payload received when the character's inventory or equipment changes
/// (e.g. after equipping, unequipping, looting, buying, selling, or dropping an item).
/// The server sends the full inventory and equipment snapshot.
/// </summary>
/// <param name="InventoryItems">All items currently in the character's inventory bag.</param>
/// <param name="EquippedItems">All items currently equipped, keyed by slot name (Head, Chest, MainHand, etc.).</param>
public sealed record InventoryUpdatedPayload(
    List<Item> InventoryItems,
    Dictionary<string, Item> EquippedItems);

/// <summary>
/// Hub event payload received when a single equipment slot changes
/// (e.g. after equipping or unequipping an item).
/// </summary>
/// <param name="Slot">The equipment slot name (Head, Chest, MainHand, OffHand, Legs, Feet, Ring, Amulet).</param>
/// <param name="Item">The item now in the slot, or <c>null</c> if the slot was unequipped.</param>
public sealed record EquipmentChangedPayload(
    string Slot,
    Item? Item);

/// <summary>
/// Hub event payload received when the merchant's shop catalog is available.
/// Sent when the player opens a shop interface or when the catalog is refreshed.
/// </summary>
/// <param name="Catalog">The items available for purchase from the current merchant.</param>
public sealed record ShopCatalogPayload(
    List<ShopItemEntry> Catalog);
