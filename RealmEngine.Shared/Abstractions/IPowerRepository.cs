using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>
/// Repository interface for reading power catalog data.
/// Replaces the former <c>IAbilityRepository</c> and <c>ISpellRepository</c>.
/// </summary>
public interface IPowerRepository
{
    /// <summary>Returns all active powers.</summary>
    Task<List<Power>> GetAllAsync();

    /// <summary>Returns a single power by slug (e.g. "fireball", "power-strike").</summary>
    Task<Power?> GetBySlugAsync(string slug);

    /// <summary>Returns all powers of a given acquisition type ("innate", "talent", "spell", "cantrip", "ultimate", "passive", "reaction").</summary>
    Task<List<Power>> GetByTypeAsync(string powerType);

    /// <summary>Returns all powers belonging to a given school/tradition ("fire", "arcane", "divine", etc.).</summary>
    Task<List<Power>> GetBySchoolAsync(string school);
}
