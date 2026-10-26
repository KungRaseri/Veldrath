namespace RealmEngine.Data.Validation.Content;

/// <summary>
/// Stable rule ids emitted by content validation.
/// </summary>
/// <remarks>
/// The ids are the contract between the startup audit, the write path and the CI guard — changing a value breaks
/// "same defect, same rule" and must be treated as a breaking change. The four reference ids keep the values the
/// audit used before the rules moved into this library, so existing logs stay comparable.
/// </remarks>
public static class ContentRules
{
    /// <summary>The entity has no slug.</summary>
    public const string SlugMissing = "slug_missing";

    /// <summary>The slug is present but not lowercase-hyphenated.</summary>
    public const string SlugFormat = "slug_format";

    /// <summary>The entity has no human-readable display name.</summary>
    public const string DisplayNameMissing = "display_name_missing";

    /// <summary>The entity has no type key.</summary>
    public const string TypeKeyMissing = "type_key_missing";

    /// <summary>The rarity weight falls outside the documented 1–100 range.</summary>
    public const string RarityWeightOutOfRange = "rarity_weight_out_of_range";

    /// <summary>The two halves of a min/max pair are inverted or negative.</summary>
    public const string RangeInvalid = "range_invalid";

    /// <summary>A required discriminator such as <c>itemType</c> or <c>powerType</c> is empty.</summary>
    public const string DiscriminatorMissing = "discriminator_missing";

    /// <summary>A discriminator carries a value that no consumer handles.</summary>
    public const string DiscriminatorUnknown = "discriminator_unknown";

    /// <summary>A field that only makes sense together with another is set without it.</summary>
    public const string FieldInconsistent = "field_inconsistent";

    /// <summary>A numeric field is negative or zero where that makes no sense.</summary>
    public const string ValueOutOfRange = "value_out_of_range";

    /// <summary>A quest declares no objectives, so it could be completed instantly.</summary>
    public const string QuestWithoutObjectives = "quest_without_objectives";

    /// <summary>A quest objective names no key or a quantity below one.</summary>
    public const string QuestObjectiveInvalid = "quest_objective_invalid";

    /// <summary>An objective points at content that does not exist.</summary>
    public const string MissingQuestTarget = "missing-quest-target";

    /// <summary>An objective uses a kind that no gameplay call site implements.</summary>
    public const string UnsupportedQuestKind = "unsupported-quest-kind";

    /// <summary>A spawn pool points at an actor archetype that does not exist.</summary>
    public const string MissingSpawnArchetype = "missing-spawn-archetype";

    /// <summary>A map edge points at a node that is not on its map.</summary>
    public const string MissingMapNode = "missing-map-node";

    /// <summary>A loot table has no entries, so it can never drop anything.</summary>
    public const string LootTableWithoutEntries = "loot_table_without_entries";

    /// <summary>A loot table entry points at an item that does not exist.</summary>
    public const string MissingLootItem = "missing-loot-item";

    /// <summary>A loot table entry has a weight of zero or less, so it is unreachable.</summary>
    public const string LootWeightInvalid = "loot_weight_invalid";

    /// <summary>A recipe produces nothing, or an ingredient points at an item that does not exist.</summary>
    public const string RecipeInconsistent = "recipe_inconsistent";

    /// <summary>A zone location's spawn pool is empty. Warning only: fully safe locations are legitimate.</summary>
    public const string SpawnPoolEmpty = "spawn_pool_empty";

    /// <summary>A location's level band is inverted.</summary>
    public const string LevelBandInvalid = "level_band_invalid";

    /// <summary>An actor archetype's level range is inverted, or its reward band is inverted.</summary>
    public const string ArchetypeRangeInvalid = "archetype_range_invalid";

    /// <summary>An actor instance has no archetype.</summary>
    public const string InstanceWithoutArchetype = "instance_without_archetype";

    /// <summary>A dialogue has no lines, so it can never be spoken.</summary>
    public const string DialogueWithoutLines = "dialogue_without_lines";
}
