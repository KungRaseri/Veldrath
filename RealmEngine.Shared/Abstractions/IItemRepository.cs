using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>
/// Repository interface for accessing general item catalog data
/// (consumables, crystals, gems, runes, essences, orbs).
/// </summary>
public interface IItemRepository
{
    /// <summary>Gets all active items in the catalog.</summary>
    Task<List<Item>> GetAllAsync();

    /// <summary>Gets a specific item by its slug. Returns <see langword="null"/> if not found or inactive.</summary>
    Task<Item?> GetBySlugAsync(string slug);

    /// <summary>Gets all active items that belong to a given category (e.g. "consumable", "gem", "rune").</summary>
    Task<List<Item>> GetByTypeAsync(string itemType);
}
