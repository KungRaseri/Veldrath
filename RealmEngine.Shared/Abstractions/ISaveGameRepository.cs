using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>
/// Repository interface for managing save game data.
/// </summary>
public interface ISaveGameRepository
{
    /// <summary>Saves a game to the repository.</summary>
    void SaveGame(SaveGame saveGame);
    
    /// <summary>Loads a game from the specified slot.</summary>
    SaveGame? LoadGame(int slot);
    
    /// <summary>Gets a save game by its unique identifier.</summary>
    SaveGame? GetById(string id);
    
    /// <summary>Gets the most recently saved game.</summary>
    SaveGame? GetMostRecent();
    
    /// <summary>Gets all save games.</summary>
    List<SaveGame> GetAll();
    
    /// <summary>Gets all save games for a specific player.</summary>
    List<SaveGame> GetByPlayerName(string playerName);
    
    /// <summary>Deletes a save game by its identifier.</summary>
    bool Delete(string id);
    
    /// <summary>Deletes a save game from the specified slot.</summary>
    bool DeleteSave(int slot);
    
    /// <summary>Checks if a save exists in the specified slot.</summary>
    bool SaveExists(int slot);
}
