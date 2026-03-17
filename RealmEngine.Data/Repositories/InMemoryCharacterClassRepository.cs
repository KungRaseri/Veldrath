using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// In-memory character class repository that returns empty data. Used when no database is configured (tests, in-memory mode).
/// </summary>
public class InMemoryCharacterClassRepository : ICharacterClassRepository
{
    /// <inheritdoc />
    public List<CharacterClass> GetAll() => [];

    /// <inheritdoc />
    public List<CharacterClass> GetClassesByType(string classType) => [];

    /// <inheritdoc />
    public List<CharacterClass> GetBaseClasses() => [];

    /// <inheritdoc />
    public List<CharacterClass> GetSubclasses() => [];

    /// <inheritdoc />
    public List<CharacterClass> GetSubclassesForParent(string parentClassId) => [];

    /// <inheritdoc />
    public CharacterClass? GetById(string id) => null;

    /// <inheritdoc />
    public CharacterClass? GetByName(string name) => null;

}
