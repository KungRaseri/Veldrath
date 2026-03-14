using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>Repository interface for reading enemy catalog data.</summary>
public interface IEnemyRepository
{
    /// <summary>Returns all active enemies.</summary>
    Task<List<Enemy>> GetAllAsync();

    /// <summary>Returns a single enemy by slug.</summary>
    Task<Enemy?> GetBySlugAsync(string slug);

    /// <summary>Returns all enemies in a given family/category TypeKey (e.g. "wolves", "humanoids/bandits").</summary>
    Task<List<Enemy>> GetByFamilyAsync(string family);
}
