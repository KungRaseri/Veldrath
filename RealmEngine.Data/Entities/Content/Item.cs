namespace RealmEngine.Data.Entities;

/// <summary>
/// General items — consumables, crystals, essences, gems, orbs, runes.
/// TypeKey = item category (e.g. "consumables", "gems", "runes").
/// </summary>
public class Item : ContentBase
{
    /// <summary>"consumable" | "crystal" | "gem" | "rune" | "essence" | "orb"</summary>
    public string ItemType { get; set; } = string.Empty;

    /// <summary>Weight, stack, and effect statistics.</summary>
    public ItemStats Stats { get; set; } = new();
    /// <summary>Boolean trait flags for this item.</summary>
    public ItemTraits Traits { get; set; } = new();
}

/// <summary>Weight, stack, and effect statistics owned by an Item.</summary>
public class ItemStats
{
    /// <summary>Item weight in lbs — affects encumbrance.</summary>
    public float? Weight { get; set; }
    /// <summary>Maximum number of identical items that can occupy one inventory slot.</summary>
    public int? StackSize { get; set; }
    /// <summary>Base sell/buy value in gold.</summary>
    public int? Value { get; set; }
    /// <summary>Magnitude of the item's primary effect (e.g. heal amount, buff power).</summary>
    public float? EffectPower { get; set; }
    /// <summary>Duration of the item's effect in seconds.</summary>
    public float? Duration { get; set; }
}

/// <summary>Boolean trait flags owned by an Item.</summary>
public class ItemTraits
{
    /// <summary>True if multiple of this item can occupy a single inventory slot.</summary>
    public bool? Stackable { get; set; }
    /// <summary>True if this item is required by a quest and cannot be discarded.</summary>
    public bool? QuestItem { get; set; }
    /// <summary>True if only one copy can exist per character.</summary>
    public bool? Unique { get; set; }
    /// <summary>True if the item binds to the character on pickup and cannot be traded.</summary>
    public bool? Soulbound { get; set; }
    /// <summary>True if the item is destroyed on use.</summary>
    public bool? Consumable { get; set; }
    /// <summary>True if the item has a magical effect.</summary>
    public bool? Magical { get; set; }
}
