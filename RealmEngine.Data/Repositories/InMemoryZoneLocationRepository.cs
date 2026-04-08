using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// In-memory stub implementation of <see cref="IZoneLocationRepository"/>.
/// Returns empty results — used in the InMemory (no-database) DI configuration path.
/// </summary>
public class InMemoryZoneLocationRepository : IZoneLocationRepository
{
    /// <inheritdoc />
    public Task<List<ZoneLocationEntry>> GetAllAsync() =>
        Task.FromResult(new List<ZoneLocationEntry>());

    /// <inheritdoc />
    public Task<ZoneLocationEntry?> GetBySlugAsync(string slug) =>
        Task.FromResult((ZoneLocationEntry?)null);

    /// <inheritdoc />
    public Task<List<ZoneLocationEntry>> GetByTypeKeyAsync(string typeKey) =>
        Task.FromResult(new List<ZoneLocationEntry>());

    /// <inheritdoc />
    public Task<List<ZoneLocationEntry>> GetByZoneIdAsync(string zoneId) =>
        Task.FromResult(new List<ZoneLocationEntry>());

    /// <inheritdoc />
    public Task<List<ZoneLocationEntry>> GetByZoneIdAsync(string zoneId, IEnumerable<string> unlockedSlugs) =>
        Task.FromResult(new List<ZoneLocationEntry>());

    /// <inheritdoc />
    public Task<List<ZoneLocationEntry>> GetHiddenByZoneIdAsync(string zoneId) =>
        Task.FromResult(new List<ZoneLocationEntry>());
}
