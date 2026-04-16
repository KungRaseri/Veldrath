using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>Repository interface for reading language catalog data.</summary>
public interface ILanguageRepository
{
    /// <summary>Returns all active languages.</summary>
    Task<List<Language>> GetAllAsync();

    /// <summary>Returns a single language by slug.</summary>
    Task<Language?> GetBySlugAsync(string slug);

    /// <summary>Returns all languages belonging to a given type key (e.g. "imperial", "elven").</summary>
    Task<List<Language>> GetByTypeKeyAsync(string typeKey);
}
