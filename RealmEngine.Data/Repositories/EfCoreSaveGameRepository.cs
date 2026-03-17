using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ISaveGameRepository"/> backed by <see cref="GameDbContext"/>.
/// <see cref="SaveGame"/> objects are serialised as JSON and stored in a <see cref="SaveGameRecord"/>
/// so the complex object graph does not require relational decomposition.
/// </summary>
public class EfCoreSaveGameRepository : ISaveGameRepository
{
    private readonly GameDbContext _db;

    /// <summary>Initialises a new <see cref="EfCoreSaveGameRepository"/> with the given context.</summary>
    public EfCoreSaveGameRepository(GameDbContext db) => _db = db;

    /// <inheritdoc/>
    public void SaveGame(SaveGame saveGame)
    {
        var existing = _db.SaveGames.Find(saveGame.Id);
        var record = ToRecord(saveGame);

        if (existing is null)
            _db.SaveGames.Add(record);
        else
        {
            existing.PlayerName = record.PlayerName;
            existing.SaveDate   = record.SaveDate;
            existing.DataJson   = record.DataJson;
        }

        _db.SaveChanges();
    }

    /// <inheritdoc/>
    public SaveGame? LoadGame(int slot)
    {
        var record = _db.SaveGames.AsNoTracking().FirstOrDefault(s => s.SlotIndex == slot);
        return record is null ? null : FromRecord(record);
    }

    /// <inheritdoc/>
    public SaveGame? GetById(string id)
    {
        var record = _db.SaveGames.Find(id);
        return record is null ? null : FromRecord(record);
    }

    /// <inheritdoc/>
    public SaveGame? GetMostRecent()
    {
        var record = _db.SaveGames.AsNoTracking().OrderByDescending(s => s.SaveDate).FirstOrDefault();
        return record is null ? null : FromRecord(record);
    }

    /// <inheritdoc/>
    public List<SaveGame> GetAll() =>
        _db.SaveGames.AsNoTracking().AsEnumerable().Select(FromRecord).ToList();

    /// <inheritdoc/>
    public List<SaveGame> GetByPlayerName(string playerName) =>
        _db.SaveGames.AsNoTracking()
            .Where(s => s.PlayerName == playerName)
            .AsEnumerable()
            .Select(FromRecord)
            .ToList();

    /// <inheritdoc/>
    public bool Delete(string id)
    {
        var record = _db.SaveGames.Find(id);
        if (record is null) return false;
        _db.SaveGames.Remove(record);
        _db.SaveChanges();
        return true;
    }

    /// <inheritdoc/>
    public bool DeleteSave(int slot)
    {
        var record = _db.SaveGames.FirstOrDefault(s => s.SlotIndex == slot);
        if (record is null) return false;
        _db.SaveGames.Remove(record);
        _db.SaveChanges();
        return true;
    }

    /// <inheritdoc/>
    public bool SaveExists(int slot) => _db.SaveGames.Any(s => s.SlotIndex == slot);

    // ── helpers ──────────────────────────────────────────────────────────────

    private static SaveGameRecord ToRecord(SaveGame sg) => new()
    {
        Id         = sg.Id,
        PlayerName = sg.PlayerName,
        SaveDate   = sg.SaveDate,
        DataJson   = JsonConvert.SerializeObject(sg),
    };

    private static SaveGame FromRecord(SaveGameRecord r) =>
        JsonConvert.DeserializeObject<SaveGame>(r.DataJson) ?? new SaveGame { Id = r.Id };
}
