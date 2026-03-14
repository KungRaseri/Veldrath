using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>Repository interface for reading quest catalog data.</summary>
public interface IQuestRepository
{
    /// <summary>Returns all active quests.</summary>
    Task<List<Quest>> GetAllAsync();

    /// <summary>Returns a single quest by slug.</summary>
    Task<Quest?> GetBySlugAsync(string slug);

    /// <summary>Returns all quests of a given TypeKey (e.g. "main-story", "side", "repeatable").</summary>
    Task<List<Quest>> GetByTypeKeyAsync(string typeKey);
}
