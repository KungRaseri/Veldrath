using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>
/// Repository interface for managing character classes from JSON data.
/// </summary>
public interface ICharacterClassRepository : IDisposable
{
    /// <summary>Gets a character class by its unique identifier.</summary>
    /// <param name="id">The character class identifier.</param>
    /// <returns>The character class if found; otherwise null.</returns>
    CharacterClass? GetById(string id);
    
    /// <summary>Gets a character class by its name.</summary>
    /// <param name="name">The character class name.</param>
    /// <returns>The character class if found; otherwise null.</returns>
    CharacterClass? GetByName(string name);
    
    /// <summary>Gets a character class by its name (alias for GetByName).</summary>
    /// <param name="name">The character class name.</param>
    /// <returns>The character class if found; otherwise null.</returns>
    CharacterClass? GetClassByName(string name);
    
    /// <summary>Gets all character classes.</summary>
    /// <returns>A list of all character classes.</returns>
    List<CharacterClass> GetAll();
    
    /// <summary>Gets all character classes (alias for GetAll).</summary>
    /// <returns>A list of all character classes.</returns>
    List<CharacterClass> GetAllClasses();
    
    /// <summary>Gets all classes of a specific type/category (e.g., "warrior", "mage", "cleric").</summary>
    /// <param name="classType">The class type/category to filter by.</param>
    /// <returns>A list of character classes of the specified type.</returns>
    List<CharacterClass> GetClassesByType(string classType);
    
    /// <summary>Gets only base classes (excluding subclasses).</summary>
    /// <returns>A list of base character classes.</returns>
    List<CharacterClass> GetBaseClasses();
    
    /// <summary>Gets only subclasses.</summary>
    /// <returns>A list of subclass character classes.</returns>
    List<CharacterClass> GetSubclasses();
    
    /// <summary>Gets subclasses for a specific parent class.</summary>
    /// <param name="parentClassId">The parent class ID (e.g., "cleric:Priest").</param>
    /// <returns>A list of subclasses for the specified parent.</returns>
    List<CharacterClass> GetSubclassesForParent(string parentClassId);
    
    /// <summary>Adds a new character class to the repository.</summary>
    /// <param name="characterClass">The character class to add.</param>
    void Add(CharacterClass characterClass);
    
    /// <summary>Updates an existing character class in the repository.</summary>
    /// <param name="characterClass">The character class to update.</param>
    void Update(CharacterClass characterClass);
    
    /// <summary>Deletes a character class from the repository.</summary>
    /// <param name="id">The identifier of the character class to delete.</param>
    void Delete(string id);
}
