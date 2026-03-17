using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>
/// Repository interface for managing Hall of Fame entries.
/// </summary>
public interface IHallOfFameRepository
{
    /// <summary>Adds a new entry to the Hall of Fame.</summary>
    /// <param name="entry">The Hall of Fame entry to add.</param>
    void AddEntry(HallOfFameEntry entry);
    
    /// <summary>Gets all Hall of Fame entries.</summary>
    /// <param name="limit">Maximum number of entries to return.</param>
    /// <returns>A list of Hall of Fame entries.</returns>
    List<HallOfFameEntry> GetAllEntries(int limit = 100);
    
    /// <summary>Gets the top heroes from the Hall of Fame.</summary>
    /// <param name="count">Number of top heroes to return.</param>
    /// <returns>A list of the top heroes.</returns>
    List<HallOfFameEntry> GetTopHeroes(int count = 10);
}
