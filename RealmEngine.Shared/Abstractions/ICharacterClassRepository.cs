using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>
/// Repository interface for reading character class catalog data.
/// </summary>
public interface ICharacterClassRepository
{
    /// <summary>Gets a character class by its unique identifier.</summary>
    CharacterClass? GetById(string id);
    
    /// <summary>Gets a character class by its name.</summary>
    CharacterClass? GetByName(string name);
    
    /// <summary>Gets all character classes.</summary>
    List<CharacterClass> GetAll();
    
    /// <summary>Gets all classes of a specific type/category (e.g., "warrior", "mage", "cleric").</summary>
    List<CharacterClass> GetClassesByType(string classType);
    
    /// <summary>Gets only base classes (excluding subclasses).</summary>
    List<CharacterClass> GetBaseClasses();
    
    /// <summary>Gets only subclasses.</summary>
    List<CharacterClass> GetSubclasses();
    
    /// <summary>Gets subclasses for a specific parent class.</summary>
    List<CharacterClass> GetSubclassesForParent(string parentClassId);
}
