using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmUnbound.Server.Data.Repositories;

/// <summary>
/// Server-side implementation of <see cref="ISaveGameRepository"/> backed by
/// <see cref="GameDbContext"/> (Postgres prod, SQLite tests).
/// Registered when <c>AddRealmEngineCore(p =&gt; p.UseExternal())</c> is used.
/// </summary>
public class ServerSaveGameRepository : ISaveGameRepository
{
    private readonly GameDbContext _db;

    /// <summary>Initializes a new instance of <see cref="ServerSaveGameRepository"/>.</summary>
    public ServerSaveGameRepository(GameDbContext db) => _db = db;

    public void Save(SaveGame saveGame)
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

    public void SaveGame(SaveGame saveGame) => Save(saveGame);

    public SaveGame? LoadGame(int slot)
    {
        var record = _db.SaveGames.FirstOrDefault(s => s.SlotIndex == slot);
        return record is null ? null : FromRecord(record);
    }

    public SaveGame? GetById(string id)
    {
        var record = _db.SaveGames.Find(id);
        return record is null ? null : FromRecord(record);
    }

    public SaveGame? GetMostRecent()
    {
        var record = _db.SaveGames.OrderByDescending(s => s.SaveDate).FirstOrDefault();
        return record is null ? null : FromRecord(record);
    }

    public List<SaveGame> GetAll() =>
        _db.SaveGames.AsNoTracking().AsEnumerable().Select(FromRecord).ToList();

    public List<SaveGame> GetAllSaves() => GetAll();

    public List<SaveGame> GetByPlayerName(string playerName) =>
        _db.SaveGames.AsNoTracking()
            .Where(s => s.PlayerName == playerName)
            .AsEnumerable()
            .Select(FromRecord)
            .ToList();

    public bool Delete(string id)
    {
        var record = _db.SaveGames.Find(id);
        if (record is null) return false;
        _db.SaveGames.Remove(record);
        _db.SaveChanges();
        return true;
    }

    public bool DeleteSave(int slot)
    {
        var record = _db.SaveGames.FirstOrDefault(s => s.SlotIndex == slot);
        if (record is null) return false;
        _db.SaveGames.Remove(record);
        _db.SaveChanges();
        return true;
    }

    public bool SaveExists(int slot) => _db.SaveGames.Any(s => s.SlotIndex == slot);

    public void Dispose() { }

    private static SaveGameRecord ToRecord(SaveGame sg) => new()
    {
        Id         = sg.Id,
        PlayerName = sg.PlayerName,
        SaveDate   = sg.SaveDate,
        DataJson   = JsonSerializer.Serialize(sg),
    };

    private static SaveGame FromRecord(SaveGameRecord r) =>
        JsonSerializer.Deserialize<SaveGame>(r.DataJson) ?? new SaveGame { Id = r.Id };
}
