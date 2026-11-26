using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace Veldrath.Server.Tests.Infrastructure;

/// <summary>
/// In-memory zone location catalog that applies the same rules as <c>EfCoreZoneLocationRepository</c>: only active
/// rows are returned, and the plain zone overload hides locations flagged <c>IsHidden</c> unless the character's
/// unlocked slugs name them. Tests use it where the hidden/locked distinction is the behaviour under test.
/// </summary>
public sealed class FakeZoneLocationRepository : IZoneLocationRepository
{
    private readonly List<ZoneLocationEntry> _locations;
    private readonly HashSet<string> _inactiveSlugs;

    /// <summary>Initializes a new instance over the given authored locations.</summary>
    /// <param name="locations">Authoring rows the fake serves.</param>
    /// <param name="inactiveSlugs">Slugs to treat as inactive, as the real catalog's <c>IsActive</c> filter does.</param>
    public FakeZoneLocationRepository(IEnumerable<ZoneLocationEntry> locations, params string[] inactiveSlugs)
    {
        _locations = [.. locations];
        _inactiveSlugs = [.. inactiveSlugs];
    }

    /// <inheritdoc />
    public Task<List<ZoneLocationEntry>> GetAllAsync() =>
        Task.FromResult(_locations.Where(IsActive).ToList());

    /// <inheritdoc />
    public Task<ZoneLocationEntry?> GetBySlugAsync(string slug) =>
        Task.FromResult(_locations.FirstOrDefault(location =>
            IsActive(location) && string.Equals(location.Slug, slug, StringComparison.Ordinal)));

    /// <inheritdoc />
    public Task<List<ZoneLocationEntry>> GetByTypeKeyAsync(string typeKey) =>
        Task.FromResult(_locations
            .Where(location => IsActive(location) &&
                string.Equals(location.TypeKey, typeKey, StringComparison.OrdinalIgnoreCase))
            .ToList());

    /// <inheritdoc />
    public Task<List<ZoneLocationEntry>> GetByZoneIdAsync(string zoneId) =>
        Task.FromResult(InZone(zoneId).Where(location => !location.IsHidden).ToList());

    /// <inheritdoc />
    public Task<List<ZoneLocationEntry>> GetByZoneIdAsync(string zoneId, IEnumerable<string> unlockedSlugs)
    {
        var unlocked = unlockedSlugs.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(InZone(zoneId)
            .Where(location => !location.IsHidden || unlocked.Contains(location.Slug))
            .ToList());
    }

    /// <inheritdoc />
    public Task<List<ZoneLocationEntry>> GetHiddenByZoneIdAsync(string zoneId) =>
        Task.FromResult(InZone(zoneId).Where(location => location.IsHidden).ToList());

    /// <inheritdoc />
    public Task<List<ZoneLocationEntry>> GetAllByZoneIdAsync(string zoneId) =>
        Task.FromResult(InZone(zoneId).ToList());

    private IEnumerable<ZoneLocationEntry> InZone(string zoneId) =>
        _locations.Where(location => IsActive(location) &&
            string.Equals(location.ZoneId, zoneId, StringComparison.OrdinalIgnoreCase));

    private bool IsActive(ZoneLocationEntry location) => !_inactiveSlugs.Contains(location.Slug);
}
