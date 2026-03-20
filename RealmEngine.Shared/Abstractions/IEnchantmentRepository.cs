using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>
/// Repository interface for accessing enchantment catalog data.
/// </summary>
public interface IEnchantmentRepository
{
    /// <summary>Gets all active enchantments in the catalog.</summary>
    Task<List<Enchantment>> GetAllAsync();

    /// <summary>Gets a specific enchantment by its slug. Returns <see langword="null"/> if not found or inactive.</summary>
    Task<Enchantment?> GetBySlugAsync(string slug);

    /// <summary>Gets all active enchantments that target a given equipment slot (e.g. "weapon", "armor", "any").</summary>
    Task<List<Enchantment>> GetByTargetSlotAsync(string targetSlot);
}
