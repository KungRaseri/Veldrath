using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Crafting.Commands;

/// <summary>
/// Command to teach a character a new crafting recipe.
/// Used when learning from trainers, quest rewards, or recipe scrolls.
/// </summary>
public class LearnRecipeCommand : IRequest<LearnRecipeResult>
{
    /// <summary>
    /// The character learning the recipe.
    /// </summary>
    public required Character Character { get; set; }

    /// <summary>
    /// The recipe to learn (by ID or slug).
    /// </summary>
    public required string RecipeId { get; set; }

    /// <summary>
    /// Optional: The source of learning (Trainer, Quest, Scroll, Discovery).
    /// </summary>
    public string? Source { get; set; }
}

/// <summary>
/// Result of attempting to learn a recipe.
/// </summary>
public class LearnRecipeResult
{
    /// <summary>Whether the recipe was successfully learned.</summary>
    public bool Success { get; set; }

    /// <summary>Message describing the result.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Name of the recipe that was learned (if successful).</summary>
    public string? RecipeName { get; set; }
}
