using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.ItemGeneration.Commands;

/// <summary>
/// Command to generate random items from any category or across all categories.
/// Supports batch generation with quantity control.
/// </summary>
public class GenerateRandomItemsCommand : IRequest<GenerateRandomItemsResult>
{
    /// <summary>
    /// The number of items to generate. Defaults to 1.
    /// </summary>
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Optional category filter (e.g., "weapons/swords", "armor", "consumables").
    /// If null or "random", items will be selected from all available categories.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Minimum budget per item (default: 10).
    /// </summary>
    public int MinBudget { get; set; } = 10;

    /// <summary>
    /// Maximum budget per item (default: 50).
    /// </summary>
    public int MaxBudget { get; set; } = 50;

    /// <summary>
    /// If true, resolves references and populates full item details (default: true).
    /// </summary>
    public bool Hydrate { get; set; } = true;

    /// <summary>
    /// If true, uses budget-based generation with materials/enchantments (default: true).
    /// If false, uses simple catalog-based generation.
    /// </summary>
    public bool UseBudgetGeneration { get; set; } = true;
}

/// <summary>
/// Result of generating random items.
/// </summary>
public class GenerateRandomItemsResult
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

    /// <summary>
    /// Categories that were considered for random selection.
    /// </summary>
    public List<string> CategoriesUsed { get; set; } = new();
}
