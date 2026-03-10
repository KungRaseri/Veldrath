using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Serilog;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IHallOfFameRepository"/> backed by <see cref="GameDbContext"/>.
/// <see cref="HallOfFameEntry"/> maps directly to a table because all its properties are primitives.
/// </summary>
public class EfCoreHallOfFameRepository : IHallOfFameRepository
{
    private readonly GameDbContext _db;

    /// <summary>Initialises a new <see cref="EfCoreHallOfFameRepository"/> with the given context.</summary>
    public EfCoreHallOfFameRepository(GameDbContext db) => _db = db;

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public List<HallOfFameEntry> GetAllEntries(int limit = 100) =>
        _db.HallOfFameEntries.AsNoTracking()
            .OrderByDescending(e => e.FameScore)
            .Take(limit)
            .ToList();

    /// <inheritdoc/>
    public List<HallOfFameEntry> GetTopHeroes(int count = 10) =>
        _db.HallOfFameEntries.AsNoTracking()
            .OrderByDescending(e => e.FameScore)
            .Take(count)
            .ToList();

    /// <inheritdoc/>
    public void Dispose() { }
}
