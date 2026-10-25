using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Crafting.Commands;

/// <summary>
/// Command to craft an item from a recipe.
/// </summary>
public record CraftRecipeCommand : IRequest<CraftRecipeResult>
{
    /// <summary>
    /// Gets the character performing the crafting.
    /// </summary>
    public required Character Character { get; init; }

    /// <summary>
    /// Gets the recipe to craft.
    /// </summary>
    public required Recipe Recipe { get; init; }

    /// <summary>
    /// Gets the crafting station being used.
    /// </summary>
    public required CraftingStation Station { get; init; }
}

/// <summary>
/// Result of crafting an item.
/// </summary>
public record CraftRecipeResult
{
    /// <summary>
    /// Gets a value indicating whether the crafting was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets a message describing the crafting result.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the crafted item (null if crafting failed).
    /// </summary>
    public Item? CraftedItem { get; init; }

    /// <summary>
    /// Gets the quality of the crafted item (0-100).
    /// </summary>
    public int Quality { get; init; }

    /// <summary>
    /// Gets the skill experience gained from crafting.
    /// </summary>
    public int SkillXpGained { get; init; }

    /// <summary>
    /// Gets the materials that were consumed (for logging/display).
    /// </summary>
    public List<string> MaterialsConsumed { get; init; } = new();
}
