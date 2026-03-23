namespace RealmEngine.Data.Entities;

/// <summary>
/// General items — consumables, crystals, essences, gems, orbs, runes, weapons, and armor.
/// TypeKey = item category (e.g. "consumables", "gems", "runes", "heavy-blades", "light").
/// ItemType discriminator: "consumable" | "crystal" | "gem" | "rune" | "essence" | "orb" | "weapon" | "armor".
/// </summary>
public class Item : ContentBase
{
    /// <summary>Top-level item discriminator — "consumable" | "crystal" | "gem" | "rune" | "essence" | "orb" | "weapon" | "armor".</summary>
    public string ItemType { get; set; } = string.Empty;

    // ── Weapon-specific columns (null for non-weapons) ────────────────────────

    /// <summary>Weapon sub-type: "sword" | "axe" | "bow" | "staff" | etc. Null for non-weapons.</summary>
    public string? WeaponType { get; set; }
    /// <summary>Damage type: "physical" | "magic" | "fire" | etc. Null for non-weapons.</summary>
    public string? DamageType { get; set; }
    /// <summary>Number of hands required to wield (1 or 2). Null for non-weapons.</summary>
    public int? HandsRequired { get; set; }

    // ── Armor-specific columns (null for non-armor) ───────────────────────────

    /// <summary>Armor protection class: "light" | "medium" | "heavy" | "shield". Null for non-armor.</summary>
    public string? ArmorType { get; set; }
    /// <summary>Equipment slot: "head" | "chest" | "hands" | "feet" | etc. Null for non-armor.</summary>
    public string? EquipSlot { get; set; }

    // ── Owned types ────────────────────────────────────────────────────────────

    /// <summary>Numeric stats stored as JSON — used for all item types.</summary>
    public ItemStats Stats { get; set; } = new();
    /// <summary>Boolean trait flags stored as JSON — used for all item types.</summary>
    public ItemTraits Traits { get; set; } = new();
}

/// <summary>Numeric stats owned by an Item, serialised as a JSON column.</summary>
public class ItemStats
{
    // ── Shared across item types
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

    // ── Weapon stats
    /// <summary>Minimum damage roll. Null for non-weapons.</summary>
    public int? DamageMin { get; set; }
    /// <summary>Maximum damage roll. Null for non-weapons.</summary>
    public int? DamageMax { get; set; }
    /// <summary>Attack speed multiplier. Null for non-weapons.</summary>
    public float? AttackSpeed { get; set; }
    /// <summary>Critical hit chance (0.0–1.0). Null for non-weapons.</summary>
    public float? CritChance { get; set; }
    /// <summary>Effective attack range in metres. Null for non-weapons.</summary>
    public float? Range { get; set; }
    /// <summary>Maximum durability points (shared with armor). Null when not applicable.</summary>
    public int? Durability { get; set; }

    // ── Armor stats
    /// <summary>Base armor rating. Null for non-armor.</summary>
    public int? ArmorRating { get; set; }
    /// <summary>Magic resistance rating. Null for non-armor.</summary>
    public int? MagicResist { get; set; }
    /// <summary>Movement speed penalty factor (0.0 = none, 0.1 = 10% slower). Null for non-armor.</summary>
    public float? MovementPenalty { get; set; }
}

/// <summary>Boolean trait flags owned by an Item, serialised as a JSON column.</summary>
public class ItemTraits
{
    // ── Shared flags
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

    // ── Weapon flags
    /// <summary>True if the weapon requires both hands to wield.</summary>
    public bool? TwoHanded { get; set; }
    /// <summary>True if the weapon can be thrown.</summary>
    public bool? Throwable { get; set; }
    /// <summary>True if the weapon is silvered (effective vs certain creatures).</summary>
    public bool? Silvered { get; set; }
    /// <summary>True if the weapon has the Finesse property (use STR or DEX for attack).</summary>
    public bool? Finesse { get; set; }
    /// <summary>True if the weapon has extended reach.</summary>
    public bool? Reach { get; set; }
    /// <summary>True if the weapon is versatile (can be used one- or two-handed).</summary>
    public bool? Versatile { get; set; }

    // ── Armor flags
    /// <summary>True if wearing this armor imposes disadvantage on stealth checks.</summary>
    public bool? StealthPenalty { get; set; }
    /// <summary>True if the armor provides fire resistance.</summary>
    public bool? FireResist { get; set; }
    /// <summary>True if the armor provides cold resistance.</summary>
    public bool? ColdResist { get; set; }
    /// <summary>True if the armor provides lightning resistance.</summary>
    public bool? LightningResist { get; set; }
    /// <summary>True if the item is cursed (cannot be unequipped without a Remove Curse spell).</summary>
    public bool? Cursed { get; set; }
}
