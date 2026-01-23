using MediatR;

namespace RealmEngine.Core.Features.ItemGeneration.Queries;

/// <summary>
/// Query to get all available item categories.
/// </summary>
public class GetAvailableItemCategoriesQuery : IRequest<GetAvailableItemCategoriesResult>
{
    /// <summary>
    /// Optional filter pattern (e.g., "weapons/*" to get all weapon subcategories).
    /// </summary>
    public string? FilterPattern { get; set; }

    /// <summary>
    /// If true, includes item count per category (default: false).
    /// </summary>
    public bool IncludeItemCounts { get; set; } = false;
}

/// <summary>
/// Result containing available item categories.
/// </summary>
public class GetAvailableItemCategoriesResult
{
    /// <summary>
    /// Whether the query was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// List of available categories with metadata.
    /// </summary>
    public List<ItemCategoryInfo> Categories { get; set; } = new();

    /// <summary>
    /// Total number of categories found.
    /// </summary>
    public int TotalCategories => Categories.Count;

    /// <summary>
    /// Error message if query failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Information about an item category.
/// </summary>
public class ItemCategoryInfo
{
    /// <summary>
    /// The category path (e.g., "weapons/swords").
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the category (e.g., "Swords").
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Parent category (e.g., "weapons").
    /// </summary>
    public string? ParentCategory { get; set; }

    /// <summary>
    /// Number of items in this category (if IncludeItemCounts was true).
    /// </summary>
    public int? ItemCount { get; set; }

    /// <summary>
    /// Whether this category has a names.json file for procedural generation.
    /// </summary>
    public bool HasNamesFile { get; set; }
}
