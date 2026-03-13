using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// In-memory character class repository that returns empty data. Used when no database is configured (tests, in-memory mode).
/// </summary>
public class InMemoryCharacterClassRepository : ICharacterClassRepository
{
    /// <inheritdoc />
    public List<CharacterClass> GetAllClasses() => [];

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
    public CharacterClass? GetClassByName(string name) => null;

    /// <inheritdoc />
    public CharacterClass? GetById(string id) => null;

    /// <inheritdoc />
    public CharacterClass? GetByName(string name) => null;

    /// <inheritdoc />
    public void Add(CharacterClass entity) => throw new NotSupportedException("In-memory repository does not support mutations");

    /// <inheritdoc />
    public void Update(CharacterClass entity) => throw new NotSupportedException("In-memory repository does not support mutations");

    /// <inheritdoc />
    public void Delete(string id) => throw new NotSupportedException("In-memory repository does not support mutations");

    /// <inheritdoc />
    public void Dispose() { }
}
