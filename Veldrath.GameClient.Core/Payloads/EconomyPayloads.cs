using RealmEngine.Shared.Models;

namespace Veldrath.GameClient.Core.Payloads;

/// <summary>
/// Hub event payload received when a character equips an item.
/// </summary>
/// <param name="CharacterId">ID of the character whose equipment changed.</param>
/// <param name="Slot">The equipment slot name (Head, Chest, MainHand, OffHand, Legs, Feet, Ring, Amulet).</param>
/// <param name="ItemRef">The slug reference of the equipped item, or <see langword="null"/> when the slot was cleared.</param>
/// <param name="Equipped">The full snapshot of all currently equipped items keyed by slot name, when resolved by the server.</param>
/// <param name="AllEquippedItems">The full equipment map (slot → item ref) after the update.</param>
public sealed record ItemEquippedPayload(
    Guid CharacterId,
    string Slot,
    string? ItemRef,
    Dictionary<string, Item>? Equipped = null,
    Dictionary<string, string>? AllEquippedItems = null);

/// <summary>
/// Hub event payload received when the character purchases an item from a shop.
/// </summary>
/// <param name="ItemRef">The slug reference of the purchased item.</param>
/// <param name="Name">The display name of the purchased item.</param>
/// <param name="GoldSpent">The amount of gold spent on the purchase.</param>
/// <param name="Inventory">The full inventory snapshot after the purchase.</param>
public sealed record ItemPurchasedPayload(string ItemRef, string Name, int GoldSpent, List<Item> Inventory);

/// <summary>
/// Hub event payload received when the character sells an item to a shop.
/// </summary>
/// <param name="ItemRef">The slug reference of the sold item.</param>
/// <param name="Name">The display name of the sold item.</param>
/// <param name="GoldReceived">The amount of gold received from the sale.</param>
/// <param name="Inventory">The full inventory snapshot after the sale.</param>
public sealed record ItemSoldPayload(string ItemRef, string Name, int GoldReceived, List<Item> Inventory);

/// <summary>
/// Hub event payload received when the character drops an item from inventory.
/// </summary>
/// <param name="ItemRef">The slug reference of the dropped item.</param>
/// <param name="Inventory">The full inventory snapshot after the drop.</param>
public sealed record ItemDroppedPayload(string ItemRef, List<Item> Inventory);

/// <summary>
/// Hub event payload received when the character's inventory and equipment are loaded from the server.
/// Contains the full bag and equipment snapshots. Items that are equipped are excluded from
/// <paramref name="ItemDetails"/> so the same item is never shown in both panels.
/// </summary>
/// <param name="ItemDetails">All items currently in the character's inventory bag.</param>
/// <param name="Equipped">All items currently equipped, keyed by canonical slot name.</param>
public sealed record InventoryLoadedPayload(
    List<Item>? ItemDetails = null,
    Dictionary<string, Item>? Equipped = null);

/// <summary>
/// Hub event payload received when the character successfully crafts an item.
/// </summary>
/// <param name="RecipeName">The display name of the crafted recipe.</param>
/// <param name="GoldSpent">The amount of gold spent on crafting materials/costs.</param>
/// <param name="RemainingGold">The character's gold after crafting.</param>
public sealed record ItemCraftedPayload(string RecipeName, int GoldSpent, int RemainingGold);
