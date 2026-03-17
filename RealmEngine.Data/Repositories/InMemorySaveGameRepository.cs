using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// Pure in-memory implementation of <see cref="ISaveGameRepository"/>.
/// No file I/O — intended for unit tests and service-registration validation.
/// </summary>
public class InMemorySaveGameRepository : ISaveGameRepository
{
    private readonly Dictionary<string, SaveGame> _store = new();

    /// <inheritdoc/>
    public void SaveGame(SaveGame saveGame) => _store[saveGame.Id] = saveGame;

    /// <inheritdoc/>
    public SaveGame? LoadGame(int slot) =>
        _store.Values.FirstOrDefault(s => s.Id == slot.ToString());

    /// <inheritdoc/>
    public SaveGame? GetById(string id) =>
        _store.TryGetValue(id, out var sg) ? sg : null;

    /// <inheritdoc/>
    public SaveGame? GetMostRecent() =>
        _store.Values.OrderByDescending(s => s.SaveDate).FirstOrDefault();

    /// <inheritdoc/>
    public List<SaveGame> GetAll() => _store.Values.ToList();

    /// <inheritdoc/>
    public List<SaveGame> GetByPlayerName(string playerName) =>
        _store.Values.Where(s => s.PlayerName == playerName).ToList();

    /// <inheritdoc/>
    public bool Delete(string id) => _store.Remove(id);

    /// <inheritdoc/>
    public bool DeleteSave(int slot)
    {
        var key = slot.ToString();
        return _store.Remove(key);
    }

    /// <inheritdoc/>
    public bool SaveExists(int slot) => _store.ContainsKey(slot.ToString());

}
