using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>
/// Repository interface for accessing playable species data.
/// </summary>
public interface ISpeciesRepository
{
    /// <summary>Gets all active species available for character creation.</summary>
    Task<List<Species>> GetAllSpeciesAsync();

    /// <summary>Gets a specific species by its slug. Returns <see langword="null"/> if not found or inactive.</summary>
    Task<Species?> GetSpeciesBySlugAsync(string slug);

    /// <summary>Gets all active species belonging to a given type family (e.g. "humanoid", "beast").</summary>
    Task<List<Species>> GetSpeciesByTypeAsync(string typeKey);
}
