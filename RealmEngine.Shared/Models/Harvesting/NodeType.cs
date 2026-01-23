namespace RealmEngine.Shared.Models.Harvesting;

/// <summary>
/// Categorizes harvestable resource nodes by type and associated skill.
/// </summary>
public enum NodeType
{
    /// <summary>
    /// Ore veins containing metal ores (Mining skill).
    /// </summary>
    OreVein,

    /// <summary>
    /// Trees that yield wood and bark (Woodcutting skill).
    /// </summary>
    Tree,

    /// <summary>
    /// Herb patches containing alchemical reagents (Herbalism skill).
    /// </summary>
    HerbPatch,

    /// <summary>
    /// Fishing spots in water sources (Fishing skill).
    /// </summary>
    FishingSpot,

    /// <summary>
    /// Crystal formations with arcane crystals and gems (Mining skill, high-tier).
    /// </summary>
    CrystalFormation,

    /// <summary>
    /// Stone quarries yielding granite, marble, obsidian (Mining skill).
    /// </summary>
    StoneQuarry,

    /// <summary>
    /// Mushroom groves with fungi and spores (Herbalism skill).
    /// </summary>
    MushroomGrove,

    /// <summary>
    /// Beast corpses that can be skinned for leather and bones (Skinning skill).
    /// </summary>
    BeastCorpse
}
