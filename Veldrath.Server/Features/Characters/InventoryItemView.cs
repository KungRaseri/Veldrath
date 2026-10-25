using RealmEngine.Shared.Models;

namespace Veldrath.Server.Features.Characters;

/// <summary>
/// Flat, string-typed projection of an inventory or equipment entry for the web client.
/// </summary>
/// <remarks>
/// <para>
/// The browser renders item names, rarity colours, equipment slots, detail dialogs and bag actions,
/// so it needs string enums (the hub serialises <see cref="ItemType"/> and <see cref="ItemRarity"/> as
/// numbers by default) and the slot an item belongs to. Sending the raw <see cref="Item"/> graph would
/// leak the whole enhancement/socket model and still leave the client unable to pick a slot.
/// </para>
/// </remarks>
/// <param name="ItemRef">Item-reference slug (e.g. <c>"iron_sword"</c>).</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">Mechanical description, if the catalog has one.</param>
/// <param name="Rarity">Rarity name (e.g. <c>"Rare"</c>), always a string.</param>
/// <param name="Type">Item type name (e.g. <c>"Weapon"</c>, <c>"Consumable"</c>), always a string.</param>
/// <param name="TypeKey">Catalog category key (e.g. <c>"heavy-blades"</c>, <c>"shield"</c>).</param>
/// <param name="Slot">Canonical equipment slot the item belongs to, or <see langword="null"/> when the item is not equippable.</param>
/// <param name="Quantity">Stack size (equipment is always 1).</param>
/// <param name="StackSize">Maximum stack size from the catalog.</param>
/// <param name="IsStackable">Whether the item stacks.</param>
/// <param name="IsEquipped">Whether this entry is currently in the equipment blob.</param>
/// <param name="Price">Catalog value in gold.</param>
/// <param name="Weight">Item weight.</param>
/// <param name="Durability">Current durability (0–100) for bag entries, or <see langword="null"/>.</param>
/// <param name="Effect">Consumable effect key (e.g. <c>"heal"</c>, <c>"restore"</c>), if any.</param>
/// <param name="EffectPower">Magnitude of <paramref name="Effect"/> (0 when the item has no effect).</param>
/// <param name="IsTwoHanded">Whether the weapon occupies both hands.</param>
/// <param name="ArmorClass">Armor class (<c>light</c> / <c>medium</c> / <c>heavy</c>), if any.</param>
/// <param name="ArmorType">Armor proficiency type (e.g. <c>heavy</c>, <c>shield</c>), if any.</param>
/// <param name="WeaponType">Weapon proficiency type (e.g. <c>swords</c>), if any.</param>
/// <param name="DamageMin">Minimum weapon damage, or <see langword="null"/> for non-weapons.</param>
/// <param name="DamageMax">Maximum weapon damage, or <see langword="null"/> for non-weapons.</param>
/// <param name="RequirementsLevel">Minimum character level required to equip or use the item.</param>
/// <param name="Requirements">Minimum attribute values required, keyed by attribute name.</param>
/// <param name="Bonuses">Attribute bonuses granted while equipped, keyed by attribute name.</param>
public sealed record InventoryItemView(
    string ItemRef,
    string Name,
    string? Description,
    string Rarity,
    string Type,
    string? TypeKey,
    string? Slot,
    int Quantity,
    int StackSize,
    bool IsStackable,
    bool IsEquipped,
    int Price,
    double Weight,
    int? Durability,
    string? Effect,
    int EffectPower,
    bool IsTwoHanded,
    string? ArmorClass,
    string? ArmorType,
    string? WeaponType,
    int? DamageMin,
    int? DamageMax,
    int RequirementsLevel,
    IReadOnlyDictionary<string, int> Requirements,
    IReadOnlyDictionary<string, int> Bonuses);

/// <summary>
/// Canonical equipment slots and the mapping from catalog items to those slots.
/// </summary>
/// <remarks>
/// The slot names must stay in sync with <see cref="EquipItemHubCommandHandler.ValidSlots"/> and with
/// the <c>EquipmentBlob</c> keys written by character creation.
/// </remarks>
public static class EquipmentSlots
{
    /// <summary>Main-hand slot name.</summary>
    public const string MainHand = "MainHand";

    /// <summary>Off-hand slot name.</summary>
    public const string OffHand = "OffHand";

    /// <summary>Head slot name.</summary>
    public const string Head = "Head";

    /// <summary>Shoulders slot name.</summary>
    public const string Shoulders = "Shoulders";

    /// <summary>Chest slot name.</summary>
    public const string Chest = "Chest";

    /// <summary>Bracers slot name.</summary>
    public const string Bracers = "Bracers";

    /// <summary>Gloves slot name.</summary>
    public const string Gloves = "Gloves";

    /// <summary>Belt slot name.</summary>
    public const string Belt = "Belt";

    /// <summary>Legs slot name.</summary>
    public const string Legs = "Legs";

    /// <summary>Feet slot name.</summary>
    public const string Feet = "Feet";

    /// <summary>Ring slot name.</summary>
    public const string Ring = "Ring";

    /// <summary>Amulet slot name.</summary>
    public const string Amulet = "Amulet";

    /// <summary>
    /// Resolves the equipment slot for an item type, or <see langword="null"/> when the type is not
    /// equippable (consumables, materials, quest items).
    /// </summary>
    /// <param name="type">Item type from the catalog.</param>
    /// <returns>The canonical slot name, or <see langword="null"/>.</returns>
    public static string? ForItemType(ItemType type) => type switch
    {
        ItemType.Weapon => MainHand,
        ItemType.Shield => OffHand,
        ItemType.OffHand => OffHand,
        ItemType.Helmet => Head,
        ItemType.Shoulders => Shoulders,
        ItemType.Chest => Chest,
        ItemType.Bracers => Bracers,
        ItemType.Gloves => Gloves,
        ItemType.Belt => Belt,
        ItemType.Legs => Legs,
        ItemType.Boots => Feet,
        ItemType.Ring => Ring,
        ItemType.Necklace => Amulet,
        _ => null,
    };

    /// <summary>
    /// Resolves the equipment slot for a catalog item, preferring the explicit <see cref="Item.Type"/>
    /// and falling back to the category keys for shields.
    /// </summary>
    /// <param name="item">Catalog item to inspect.</param>
    /// <returns>The canonical slot name, or <see langword="null"/> when the item cannot be equipped.</returns>
    public static string? ForItem(Item item)
    {
        var slot = ForItemType(item.Type);
        if (slot is not null)
            return slot;

        return IsShield(item) ? OffHand : null;
    }

    private static bool IsShield(Item item) =>
        string.Equals(item.TypeKey, "shield", StringComparison.OrdinalIgnoreCase)
        || string.Equals(item.TypeKey, "shields", StringComparison.OrdinalIgnoreCase)
        || string.Equals(item.ArmorType, "shield", StringComparison.OrdinalIgnoreCase)
        || string.Equals(item.ArmorType, "shields", StringComparison.OrdinalIgnoreCase)
        || item.Slug.Contains("shield", StringComparison.OrdinalIgnoreCase);
}
