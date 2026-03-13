namespace RealmEngine.Data.Entities;

/// <summary>
/// Ingredient line of a crafting recipe. Item is a soft cross-domain reference
/// resolved via content_registry (could be a material, gem, reagent, etc.).
/// </summary>
public class RecipeIngredient
{
    /// <summary>FK to the recipe this ingredient belongs to.</summary>
    public Guid RecipeId { get; set; }

    /// <summary>Domain of the required item — resolved via content_registry.</summary>
    public string ItemDomain { get; set; } = string.Empty;
    /// <summary>Slug of the required item — resolved via content_registry.</summary>
    public string ItemSlug { get; set; } = string.Empty;

    /// <summary>Number of units of this item consumed per craft.</summary>
    public int Quantity { get; set; } = 1;
    /// <summary>True if the recipe can be crafted without this ingredient.</summary>
    public bool IsOptional { get; set; }

    /// <summary>Navigation property for the owning recipe.</summary>
    public Recipe? Recipe { get; set; }
}
