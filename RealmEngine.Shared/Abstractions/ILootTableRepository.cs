using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>Repository interface for reading loot table catalog data.</summary>
public interface ILootTableRepository
{
    /// <summary>Returns all active loot tables.</summary>
    Task<List<LootTableData>> GetAllAsync();

    /// <summary>Returns a single loot table by slug.</summary>
    Task<LootTableData?> GetBySlugAsync(string slug);

    /// <summary>Returns all loot tables for a given context TypeKey (e.g. "enemies", "chests", "harvesting").</summary>
    Task<List<LootTableData>> GetByContextAsync(string context);
}
