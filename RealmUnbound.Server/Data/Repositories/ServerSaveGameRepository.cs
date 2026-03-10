using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmUnbound.Server.Data.Repositories;

/// <summary>
/// Server-side implementation of <see cref="ISaveGameRepository"/> backed by
/// <see cref="ApplicationDbContext"/> (SQLite dev / Postgres prod).
/// Registered when <c>AddRealmEngineCore(p =&gt; p.UseExternal())</c> is used.
/// </summary>
public class ServerSaveGameRepository : ISaveGameRepository
{
    private readonly ApplicationDbContext _db;

    public ServerSaveGameRepository(ApplicationDbContext db) => _db = db;

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
        DataJson   = JsonConvert.SerializeObject(sg),
    };

    private static SaveGame FromRecord(SaveGameRecord r) =>
        JsonConvert.DeserializeObject<SaveGame>(r.DataJson) ?? new SaveGame { Id = r.Id };
}
