using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Crafting.Commands;

/// <summary>
/// Command to attempt discovering a new recipe through experimentation.
/// Success chance based on skill level and number of attempts.
/// </summary>
public class DiscoverRecipeCommand : IRequest<DiscoverRecipeResult>
{
    /// <summary>
    /// The character attempting the discovery.
    /// </summary>
    public required Character Character { get; set; }

    /// <summary>
    /// The crafting skill being used for discovery.
    /// </summary>
    public required string SkillName { get; set; }

    /// <summary>
    /// Optional: Specific station being used (affects discoverable recipes).
    /// </summary>
    public string? StationId { get; set; }

    /// <summary>
    /// Materials being experimented with (consumed on attempt).
    /// </summary>
    public List<string> ExperimentMaterials { get; set; } = new();
}

/// <summary>
/// Result of a recipe discovery attempt.
/// </summary>
public class DiscoverRecipeResult
{
    /// <summary>Whether a recipe was successfully discovered.</summary>
    public bool Success { get; set; }

    /// <summary>Message describing the result.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>The recipe that was discovered (if successful).</summary>
    public Recipe? DiscoveredRecipe { get; set; }

    /// <summary>Skill XP gained from the discovery attempt.</summary>
    public int SkillXpGained { get; set; }
}
