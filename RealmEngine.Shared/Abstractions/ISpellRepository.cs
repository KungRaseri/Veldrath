using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>Repository interface for reading spell catalog data.</summary>
public interface ISpellRepository
{
    /// <summary>Returns all active spells.</summary>
    Task<List<Spell>> GetAllAsync();

    /// <summary>Returns a single spell by slug.</summary>
    Task<Spell?> GetBySlugAsync(string slug);

    /// <summary>Returns all spells of a given school/tradition TypeKey (e.g. "fire", "arcane").</summary>
    Task<List<Spell>> GetBySchoolAsync(string school);
}
