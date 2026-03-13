using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>
/// Repository interface for accessing character background data.
/// </summary>
public interface IBackgroundRepository
{
    /// <summary>
    /// Gets all available backgrounds.
    /// </summary>
    Task<List<Background>> GetAllBackgroundsAsync();

    /// <summary>
    /// Gets a specific background by ID or slug.
    /// </summary>
    Task<Background?> GetBackgroundByIdAsync(string backgroundId);

    /// <summary>
    /// Gets backgrounds filtered by primary attribute.
    /// </summary>
    Task<List<Background>> GetBackgroundsByAttributeAsync(string attribute);
}
