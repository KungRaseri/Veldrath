using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>Repository interface for reading skill catalog data.</summary>
public interface ISkillRepository
{
    /// <summary>Returns all active skills.</summary>
    Task<List<SkillDefinition>> GetAllAsync();

    /// <summary>Returns a single skill by slug.</summary>
    Task<SkillDefinition?> GetBySlugAsync(string slug);

    /// <summary>Returns all skills in a given category (e.g. "combat", "magic", "stealth").</summary>
    Task<List<SkillDefinition>> GetByCategoryAsync(string category);
}
