using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>Repository interface for reading material property catalog data.</summary>
public interface IMaterialPropertyRepository
{
    /// <summary>Returns all active material properties.</summary>
    Task<List<MaterialPropertyEntry>> GetAllAsync();

    /// <summary>Returns a single material property by slug, or <see langword="null"/> if not found.</summary>
    Task<MaterialPropertyEntry?> GetBySlugAsync(string slug);

    /// <summary>Returns all active material properties belonging to the given family (e.g. "metal", "wood", "leather").</summary>
    Task<List<MaterialPropertyEntry>> GetByFamilyAsync(string family);
}
