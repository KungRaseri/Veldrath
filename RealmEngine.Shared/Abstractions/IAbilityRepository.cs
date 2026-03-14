using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>Repository interface for reading ability catalog data.</summary>
public interface IAbilityRepository
{
    /// <summary>Returns all active abilities.</summary>
    Task<List<Ability>> GetAllAsync();

    /// <summary>Returns a single ability by slug (e.g. "basic-attack").</summary>
    Task<Ability?> GetBySlugAsync(string slug);

    /// <summary>Returns all abilities of a given type ("active", "passive", "reactive", "ultimate").</summary>
    Task<List<Ability>> GetByTypeAsync(string abilityType);
}
