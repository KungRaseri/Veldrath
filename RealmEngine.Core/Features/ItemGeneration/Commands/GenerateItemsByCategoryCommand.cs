using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.ItemGeneration.Commands;

/// <summary>
/// Command to generate specific items by category with quantity control.
/// </summary>
public class GenerateItemsByCategoryCommand : IRequest<GenerateItemsByCategoryResult>
{
    /// <summary>
    /// The item category (e.g., "weapons/swords", "armor/heavy", "consumables/potions").
    /// Required.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// The number of items to generate. Defaults to 1.
    /// </summary>
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Optional specific item name within the category.
    /// If null, generates random items from the category.
    /// </summary>
    public string? ItemName { get; set; }

    /// <summary>
    /// Minimum budget per item for budget-based generation (default: 10).
    /// Only used if UseBudgetGeneration is true.
    /// </summary>
    public int MinBudget { get; set; } = 10;

    /// <summary>
    /// Maximum budget per item for budget-based generation (default: 50).
    /// Only used if UseBudgetGeneration is true.
    /// </summary>
    public int MaxBudget { get; set; } = 50;

    /// <summary>
    /// If true, resolves references and populates full item details (default: true).
    /// </summary>
    public bool Hydrate { get; set; } = true;

    /// <summary>
    /// If true, uses budget-based generation with materials/enchantments (default: false).
    /// If false, uses simple catalog-based generation.
    /// </summary>
    public bool UseBudgetGeneration { get; set; } = false;
}

/// <summary>
/// Result of generating items by category.
/// </summary>
public class GenerateItemsByCategoryResult
{
    /// <summary>
    /// Whether the generation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The generated items.
    /// </summary>
    public List<Item> Items { get; set; } = new();

    /// <summary>
    /// The category used for generation.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Number of items requested.
    /// </summary>
    public int RequestedQuantity { get; set; }

    /// <summary>
    /// Number of items successfully generated.
    /// </summary>
    public int ActualQuantity => Items.Count;

    /// <summary>
    /// Error message if generation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
