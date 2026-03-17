using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>Repository interface for reading material catalog data.</summary>
public interface IMaterialRepository
{
    /// <summary>Returns all active materials.</summary>
    Task<List<MaterialEntry>> GetAllAsync();

    /// <summary>Returns all active materials belonging to any of the given families.</summary>
    Task<List<MaterialEntry>> GetByFamiliesAsync(IEnumerable<string> families);

    /// <summary>Returns a single material by slug.</summary>
    Task<MaterialEntry?> GetBySlugAsync(string slug);
}
