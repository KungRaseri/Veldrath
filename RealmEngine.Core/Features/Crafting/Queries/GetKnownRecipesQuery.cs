using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Crafting.Queries;

/// <summary>
/// Query to get all recipes a character knows and can craft.
/// </summary>
public class GetKnownRecipesQuery : IRequest<GetKnownRecipesResult>
{
    /// <summary>
    /// The character whose recipes to retrieve.
    /// </summary>
    public required Character Character { get; set; }

    /// <summary>
    /// Optional: Filter by crafting station ID.
    /// </summary>
    public string? StationId { get; set; }

    /// <summary>
    /// Optional: Filter by required skill name.
    /// </summary>
    public string? SkillName { get; set; }

    /// <summary>
    /// Whether to include recipes the character doesn't have materials for.
    /// </summary>
    public bool IncludeUncraftable { get; set; } = true;
}

/// <summary>
/// Result containing the character's known recipes.
/// </summary>
public class GetKnownRecipesResult
{
    /// <summary>Whether the query was successful.</summary>
    public bool Success { get; set; }

    /// <summary>Message describing the result.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>List of recipes the character knows.</summary>
    public List<RecipeInfo> Recipes { get; set; } = new();
}

/// <summary>
/// Information about a recipe the character knows.
/// </summary>
public class RecipeInfo
{
    /// <summary>The recipe details.</summary>
    public required Recipe Recipe { get; set; }

    /// <summary>Whether the character can currently craft this recipe.</summary>
    public bool CanCraft { get; set; }

    /// <summary>List of materials the character is missing.</summary>
    public List<string> MissingMaterials { get; set; } = new();

    /// <summary>Whether the character meets the skill level requirement.</summary>
    public bool MeetsSkillRequirement { get; set; }
}
