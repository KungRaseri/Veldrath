using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// Pure in-memory implementation of <see cref="IHallOfFameRepository"/>.
/// No file I/O — intended for unit tests and service-registration validation.
/// </summary>
public class InMemoryHallOfFameRepository : IHallOfFameRepository
{
    private readonly List<HallOfFameEntry> _entries = [];

    /// <inheritdoc/>
    public void AddEntry(HallOfFameEntry entry)
    {
        entry.CalculateFameScore();
        _entries.Add(entry);
    }

    /// <inheritdoc/>
    public List<HallOfFameEntry> GetAllEntries(int limit = 100) =>
        _entries.OrderByDescending(e => e.FameScore).Take(limit).ToList();

    /// <inheritdoc/>
    public List<HallOfFameEntry> GetTopHeroes(int count = 10) =>
        _entries.OrderByDescending(e => e.FameScore).Take(count).ToList();

    /// <inheritdoc/>
    public void Dispose() { }
}
