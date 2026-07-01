using RealmEngine.Shared.Models;

namespace Veldrath.GameClient.Core.Payloads;

/// <summary>
/// Hub event payload received when the character equips an item.
/// </summary>
/// <param name="Slot">The equipment slot name (Head, Chest, MainHand, OffHand, Legs, Feet, Ring, Amulet).</param>
/// <param name="ItemRef">The slug reference of the equipped item.</param>
/// <param name="Equipped">The full snapshot of all currently equipped items keyed by slot name.</param>
public sealed record ItemEquippedPayload(string Slot, string ItemRef, Dictionary<string, Item> Equipped);

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
/// Hub event payload received when the character's inventory is loaded from the server.
/// Contains the full inventory snapshot.
/// </summary>
/// <param name="Items">All items currently in the character's inventory bag.</param>
public sealed record InventoryLoadedPayload(List<Item> Items);

/// <summary>
/// Hub event payload received when the character successfully crafts an item.
/// </summary>
/// <param name="RecipeName">The display name of the crafted recipe.</param>
/// <param name="GoldSpent">The amount of gold spent on crafting materials/costs.</param>
/// <param name="RemainingGold">The character's gold after crafting.</param>
public sealed record ItemCraftedPayload(string RecipeName, int GoldSpent, int RemainingGold);
