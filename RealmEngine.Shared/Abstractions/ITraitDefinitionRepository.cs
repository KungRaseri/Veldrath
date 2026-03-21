using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>Repository interface for reading trait definition data.</summary>
public interface ITraitDefinitionRepository
{
    /// <summary>Returns all trait definitions.</summary>
    Task<List<TraitDefinitionEntry>> GetAllAsync();

    /// <summary>Returns a single trait definition by key, or <see langword="null"/> if not found.</summary>
    Task<TraitDefinitionEntry?> GetByKeyAsync(string key);

    /// <summary>Returns all trait definitions that apply to the given entity type. Returns rows where <c>AppliesTo</c> is <c>"*"</c> or contains the entity type as a substring.</summary>
    Task<List<TraitDefinitionEntry>> GetByAppliesToAsync(string entityType);
}
