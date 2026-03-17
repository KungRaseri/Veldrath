using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Generators.Modern;

/// <summary>Generates CharacterClass instances from the database catalog.</summary>
public class CharacterClassGenerator(ICharacterClassRepository repository, ILogger<CharacterClassGenerator> logger)
{
    private readonly Random _random = new();

    /// <summary>Returns all active character classes.</summary>
    public async Task<List<CharacterClass>> GetAllClassesAsync(bool hydrate = true)
    {
        try
        {
            return await Task.FromResult(repository.GetAll());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading all character classes");
            return [];
        }
    }

    /// <summary>Returns a single character class by name.</summary>
    public async Task<CharacterClass?> GetClassByNameAsync(string name, bool hydrate = true)
    {
        try
        {
            return await Task.FromResult(repository.GetByName(name));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading character class by name {Name}", name);
            return null;
        }
    }

    /// <summary>Returns all character classes in a given category.</summary>
    public async Task<List<CharacterClass>> GetClassesByCategoryAsync(string categoryName, bool hydrate = true)
    {
        try
        {
            return await Task.FromResult(repository.GetClassesByType(categoryName));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading character classes by category {Category}", categoryName);
            return [];
        }
    }
}
