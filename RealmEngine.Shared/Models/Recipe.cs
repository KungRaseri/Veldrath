namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents a crafting recipe that can be used to create items.
/// </summary>
public class Recipe
{
    /// <summary>Gets or sets the unique recipe identifier.</summary>
    public required string Id { get; set; }

    /// <summary>Gets or sets the display name of the recipe.</summary>
    public required string Name { get; set; }

    /// <summary>Gets or sets the descriptive text for the recipe.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the recipe category (weapons, armor, consumables, enchantments, upgrades).</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Gets or sets the slug for URL-friendly identification.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Gets or sets the minimum character level required to craft this recipe.</summary>
    public int RequiredLevel { get; set; }

    /// <summary>Gets or sets the crafting station required (e.g., "blacksmith_forge", "enchanting_altar").</summary>
    public string RequiredStation { get; set; } = string.Empty;

    /// <summary>Gets or sets the minimum station tier required to craft this recipe.</summary>
    public int RequiredStationTier { get; set; } = 1;

    /// <summary>Gets or sets the crafting time in seconds.</summary>
    public int CraftingTime { get; set; }

    /// <summary>Gets or sets the list of materials required to craft this recipe.</summary>
    public List<RecipeMaterial> Materials { get; set; } = new();

    /// <summary>Gets or sets the JSON v4.1 reference to the output item (e.g., "@items/weapons/swords:iron-longsword").</summary>
    public string OutputItemReference { get; set; } = string.Empty;

    /// <summary>Gets or sets the quantity of items produced when crafting this recipe.</summary>
    public int OutputQuantity { get; set; } = 1;

    /// <summary>Gets or sets the minimum quality/rarity of the crafted item.</summary>
    public ItemRarity MinQuality { get; set; } = ItemRarity.Common;

    /// <summary>Gets or sets the maximum quality/rarity of the crafted item.</summary>
    public ItemRarity MaxQuality { get; set; } = ItemRarity.Uncommon;

    /// <summary>Gets or sets the experience points gained when crafting this recipe.</summary>
    public int ExperienceGained { get; set; }

    /// <summary>Gets or sets the required crafting skill name (e.g., "blacksmithing", "alchemy").</summary>
    public string? RequiredSkill { get; set; }

    /// <summary>Gets or sets the required skill level to craft this recipe.</summary>
    public int RequiredSkillLevel { get; set; }

    /// <summary>Gets or sets how this recipe is unlocked.</summary>
    public RecipeUnlockMethod UnlockMethod { get; set; } = RecipeUnlockMethod.Default;

    /// <summary>Gets or sets additional unlock requirements (trainer NPC ID, quest ID, skill level, etc.).</summary>
    public string? UnlockRequirement { get; set; }

    /// <summary>Gets or sets the rarity weight for weighted random selection.</summary>
    public int RarityWeight { get; set; } = 100;

    /// <summary>Gets or sets the base enchantment slots for items crafted from this recipe.</summary>
    public int BaseEnchantmentSlots { get; set; } = 0;
}

/// <summary>
/// Represents a material required for a crafting recipe.
/// </summary>
public class RecipeMaterial
{
    /// <summary>Gets or sets the JSON v4.1 reference to the required item (e.g., "@items/materials/ingots:iron-ingot").</summary>
    public required string ItemReference { get; set; }

    /// <summary>Gets or sets the quantity of this material required.</summary>
    public int Quantity { get; set; }

    /// <summary>Gets or sets whether this material adds bonus enchantment slots (catalyst).</summary>
    public bool IsEnchantmentCatalyst { get; set; } = false;

    /// <summary>Gets or sets the number of enchantment slots added if this is a catalyst.</summary>
    public int EnchantmentSlotsAdded { get; set; } = 0;
}

/// <summary>
/// Defines how a recipe is unlocked for a character.
/// </summary>
public enum RecipeUnlockMethod
{
    /// <summary>Recipe is known by default for all characters.</summary>
    Default,

    /// <summary>Recipe must be learned from an NPC trainer.</summary>
    Trainer,

    /// <summary>Recipe is discovered as loot (random drop).</summary>
    Discovery,

    /// <summary>Recipe is granted as a quest reward.</summary>
    QuestReward,

    /// <summary>Recipe is auto-unlocked when reaching a specific skill level.</summary>
    SkillLevel,

    /// <summary>Recipe is granted when earning a specific achievement.</summary>
    Achievement
}
