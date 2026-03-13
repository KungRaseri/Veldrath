namespace RealmEngine.Data.Entities;

/// <summary>
/// Crafting recipe. Output item is a soft reference resolved via content_registry.
/// Ingredients are a fully relational junction table.
/// </summary>
public class Recipe : ContentBase
{
    /// <summary>Domain of the output item — resolved via content_registry.</summary>
    public string OutputItemDomain { get; set; } = string.Empty;
    /// <summary>Slug of the item produced by this recipe.</summary>
    public string OutputItemSlug { get; set; } = string.Empty;
    /// <summary>Number of output items produced per craft.</summary>
    public int OutputQuantity { get; set; } = 1;

    /// <summary>Slug of the skill required to craft this recipe.</summary>
    public string CraftingSkill { get; set; } = string.Empty;
    /// <summary>Minimum skill rank required to craft this recipe.</summary>
    public int CraftingLevel { get; set; }

    /// <summary>Boolean trait flags for this recipe.</summary>
    public RecipeTraits Traits { get; set; } = new();

    /// <summary>Items consumed to produce the output.</summary>
    public ICollection<RecipeIngredient> Ingredients { get; set; } = [];
}

/// <summary>Boolean trait flags owned by a Recipe.</summary>
public class RecipeTraits
{
    /// <summary>True if the recipe is hidden until the player discovers it during play.</summary>
    public bool? Discoverable { get; set; }
    /// <summary>True if the recipe requires a crafting station (forge, alchemy bench, etc.).</summary>
    public bool? RequiresStation { get; set; }
    /// <summary>True if the recipe requires an open fire or furnace.</summary>
    public bool? RequiresFire { get; set; }
    /// <summary>True if this recipe falls under the Alchemy crafting skill.</summary>
    public bool? IsAlchemy { get; set; }
    /// <summary>True if this recipe falls under the Blacksmithing crafting skill.</summary>
    public bool? IsBlacksmithing { get; set; }
    /// <summary>True if this recipe falls under the Leatherworking crafting skill.</summary>
    public bool? IsLeatherworking { get; set; }
}
