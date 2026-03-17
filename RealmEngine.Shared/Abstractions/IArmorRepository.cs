using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>Repository interface for reading armor catalog data.</summary>
public interface IArmorRepository
{
    /// <summary>Returns all active armor entries as Item projections.</summary>
    Task<List<Item>> GetAllAsync();

    /// <summary>Returns a single armor entry by slug as an Item projection.</summary>
    Task<Item?> GetBySlugAsync(string slug);
}
