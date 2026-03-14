using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>Repository interface for reading NPC catalog data.</summary>
public interface INpcRepository
{
    /// <summary>Returns all active NPCs.</summary>
    Task<List<NPC>> GetAllAsync();

    /// <summary>Returns a single NPC by slug.</summary>
    Task<NPC?> GetBySlugAsync(string slug);

    /// <summary>Returns all NPCs of a given category TypeKey (e.g. "merchants", "guards").</summary>
    Task<List<NPC>> GetByCategoryAsync(string category);
}
