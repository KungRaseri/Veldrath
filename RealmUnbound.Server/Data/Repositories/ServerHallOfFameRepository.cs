using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Serilog;

namespace RealmUnbound.Server.Data.Repositories;

/// <summary>
/// Server-side implementation of <see cref="IHallOfFameRepository"/> backed by
/// <see cref="GameDbContext"/> (Postgres prod, SQLite tests).
/// Registered when <c>AddRealmEngineCore(p =&gt; p.UseExternal())</c> is used.
/// </summary>
public class ServerHallOfFameRepository : IHallOfFameRepository
{
    private readonly GameDbContext _db;

    /// <summary>Initializes a new instance of <see cref="ServerHallOfFameRepository"/>.</summary>
    public ServerHallOfFameRepository(GameDbContext db) => _db = db;

    public void AddEntry(HallOfFameEntry entry)
    {
        try
        {
            entry.CalculateFameScore();
            _db.HallOfFameEntries.Add(entry);
            _db.SaveChanges();
            Log.Information("Added {CharacterName} to Hall of Fame (Fame Score: {Score})",
                entry.CharacterName, entry.FameScore);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to add Hall of Fame entry for {CharacterName}", entry.CharacterName);
        }
    }

    public List<HallOfFameEntry> GetAllEntries(int limit = 100) =>
        _db.HallOfFameEntries.AsNoTracking()
            .OrderByDescending(e => e.FameScore)
            .Take(limit)
            .ToList();

    public List<HallOfFameEntry> GetTopHeroes(int count = 10) =>
        _db.HallOfFameEntries.AsNoTracking()
            .OrderByDescending(e => e.FameScore)
            .Take(count)
            .ToList();

    public void Dispose() { }
}
