using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IHallOfFameRepository"/> backed by <see cref="GameDbContext"/>.
/// <see cref="HallOfFameEntry"/> maps directly to a table because all its properties are primitives.
/// </summary>
public class EfCoreHallOfFameRepository(GameDbContext db, ILogger<EfCoreHallOfFameRepository> logger)
    : IHallOfFameRepository
{

    /// <inheritdoc/>
    public void AddEntry(HallOfFameEntry entry)
    {
        try
        {
            entry.CalculateFameScore();
            db.HallOfFameEntries.Add(entry);
            db.SaveChanges();
            logger.LogInformation("Added {CharacterName} to Hall of Fame (Fame Score: {Score})",
                entry.CharacterName, entry.FameScore);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add Hall of Fame entry for {CharacterName}", entry.CharacterName);
        }
    }

    /// <inheritdoc/>
    public List<HallOfFameEntry> GetAllEntries(int limit = 100) =>
        db.HallOfFameEntries.AsNoTracking()
            .OrderByDescending(e => e.FameScore)
            .Take(limit)
            .ToList();

    /// <inheritdoc/>
    public List<HallOfFameEntry> GetTopHeroes(int count = 10) =>
        db.HallOfFameEntries.AsNoTracking()
            .OrderByDescending(e => e.FameScore)
            .Take(count)
            .ToList();

}
