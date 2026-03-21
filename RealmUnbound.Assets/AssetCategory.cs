namespace RealmUnbound.Assets;

/// <summary>Logical groupings of game assets, each mapping to a subdirectory under <c>GameAssets/</c>.</summary>
public enum AssetCategory
{
    /// <summary>Enemy and monster portrait icons.</summary>
    Enemies,

    /// <summary>Weapon item icons.</summary>
    Weapons,

    /// <summary>Armour item icons.</summary>
    Armor,

    /// <summary>Potion and consumable item icons.</summary>
    Potions,

    /// <summary>Spell and skill icons, organised by magical tradition colour.</summary>
    Spells,

    /// <summary>Character class badge icons.</summary>
    Classes,

    /// <summary>UI chrome elements (frames, buttons, backgrounds, planks).</summary>
    Ui,

    /// <summary>RPG ambient audio effects (footsteps, doors, coins, etc.).</summary>
    AudioRpg,

    /// <summary>Background music loops.</summary>
    AudioMusic,

    /// <summary>Physical impact sound effects (metal, wood, punches, etc.).</summary>
    AudioImpact,

    /// <summary>UI interface sound effects (clicks, confirmations, errors, etc.).</summary>
    AudioInterface,

    /// <summary>Mining and ore-gathering resource icons.</summary>
    CraftingMining,

    /// <summary>Fishing and aquatic resource icons.</summary>
    CraftingFishing,

    /// <summary>Hunting and trapping resource icons.</summary>
    CraftingHunting,

    /// <summary>Forestry and plant harvesting resource icons.</summary>
    CraftingForest,
}
