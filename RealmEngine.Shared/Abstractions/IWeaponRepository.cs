using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>Repository interface for reading weapon catalog data.</summary>
public interface IWeaponRepository
{
    /// <summary>Returns all active weapons as Item projections.</summary>
    Task<List<Item>> GetAllAsync();

    /// <summary>Returns a single weapon by slug as an Item projection.</summary>
    Task<Item?> GetBySlugAsync(string slug);
}
