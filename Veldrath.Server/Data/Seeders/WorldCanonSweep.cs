using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Persistence;

namespace Veldrath.Server.Data.Seeders;

/// <summary>What one <see cref="WorldCanonSweep"/> run changed, so a start can log it instead of mutating content silently.</summary>
/// <param name="RowsChanged">Number of rows that had at least one field rewritten.</param>
/// <param name="FieldsChanged">Number of individual fields rewritten.</param>
/// <param name="ChangedFields">
/// Identifiers of every rewritten field, for example <c>zone:kaldrek-maw.Name</c> or
/// <c>mapnode:region:cinderplain:zone:kaldrek-maw.Description</c>.
/// </param>
/// <param name="UnresolvedFields">
/// Fields that still carry a retired token but whose id has no entry in <see cref="WorldCanonText"/>. They are
/// left exactly as they are and reported so the gap is visible instead of silent.
/// </param>
public sealed record WorldCanonSweepResult(
    int RowsChanged,
    int FieldsChanged,
    IReadOnlyList<string> ChangedFields,
    IReadOnlyList<string> UnresolvedFields);

/// <summary>
/// Rewrites world-geography text that still carries retired lore, so a database seeded before the canon rewrite
/// is corrected on the next start instead of keeping the old prose forever.
/// </summary>
/// <remarks>
/// <para>
/// Both world seeders stop as soon as their tables have rows — <see cref="ApplicationDataSeeder"/> and
/// <c>MapGraphSeeder</c> — so the seed text only ever reaches an empty database. This sweep is the path for an
/// already-seeded one, and it is guarded rather than an upsert: a field is rewritten only when its current value
/// still contains one of <see cref="WorldCanonText.RetiredTokens"/>, matched case-sensitively. Anything an author
/// edited later — including map nodes saved through the Foundry map editor, which writes the same rows — carries
/// none of those tokens and is never touched.
/// </para>
/// <para>
/// Ids are never written. They are opaque keys, and the lowercase <c>kaldrek-maw</c> and <c>kaldreks-heart</c> slugs
/// appear in map node ids, zone-location rows and character position data; the case-sensitive match is what keeps
/// them safe.
/// </para>
/// <para>
/// The sweep is provider-agnostic — plain LINQ and <c>SaveChanges</c>, no raw SQL — so tests drive it on the same
/// SQLite and in-memory contexts the seeders use. The only call site is the Postgres startup path, where the
/// migrations and the seeders also run.
/// </para>
/// </remarks>
public static class WorldCanonSweep
{
    /// <summary>
    /// Rewrites every stale world-geography field in both contexts and reports what it did.
    /// </summary>
    /// <param name="applicationDb">Context holding <c>Worlds</c>, <c>Regions</c> and <c>Zones</c>.</param>
    /// <param name="contentDb">Context holding the map graph (<c>Maps</c>, <c>MapNodes</c>) and <c>ZoneLocations</c>.</param>
    /// <param name="cancellationToken">Cancels the sweep between rows.</param>
    /// <returns>The rows and fields that were rewritten, plus any retired text left without a canon entry.</returns>
    public static async Task<WorldCanonSweepResult> SweepAsync(
        ApplicationDbContext applicationDb,
        ContentDbContext contentDb,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicationDb);
        ArgumentNullException.ThrowIfNull(contentDb);

        var changes = new List<string>();
        var unresolved = new List<string>();
        var rows = new HashSet<string>(StringComparer.Ordinal);

        var applicationChanged = await SweepApplicationDataAsync(applicationDb, changes, unresolved, rows, cancellationToken);
        if (applicationChanged)
            await applicationDb.SaveChangesAsync(cancellationToken);

        var contentChanged = await SweepContentAsync(contentDb, changes, unresolved, rows, cancellationToken);
        if (contentChanged)
            await contentDb.SaveChangesAsync(cancellationToken);

        return new WorldCanonSweepResult(rows.Count, changes.Count, changes.AsReadOnly(), unresolved.AsReadOnly());
    }

    private static async Task<bool> SweepApplicationDataAsync(
        ApplicationDbContext db,
        List<string> changes,
        List<string> unresolved,
        HashSet<string> rows,
        CancellationToken cancellationToken)
    {
        var changedRows = 0;

        foreach (var world in await db.Worlds.ToListAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var canon = string.Equals(world.Id, WorldCanonText.WorldId, StringComparison.Ordinal)
                ? WorldCanonText.World
                : null;
            var key = $"world:{world.Id}";
            var before = changes.Count;

            Rewrite($"{key}.Name", world.Name, canon?.Name, value => world.Name = value, changes, unresolved);
            Rewrite($"{key}.Description", world.Description, canon?.Description, value => world.Description = value, changes, unresolved);
            Rewrite($"{key}.Era", world.Era, canon?.Era, value => world.Era = value, changes, unresolved);

            if (changes.Count > before)
            {
                rows.Add(key);
                changedRows++;
            }
        }

        foreach (var region in await db.Regions.ToListAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var canon = Lookup(WorldCanonText.Regions, region.Id);
            var key = $"region:{region.Id}";
            var before = changes.Count;

            Rewrite($"{key}.Name", region.Name, canon?.Name, value => region.Name = value, changes, unresolved);
            Rewrite($"{key}.Description", region.Description, canon?.Description, value => region.Description = value, changes, unresolved);

            if (changes.Count > before)
            {
                rows.Add(key);
                changedRows++;
            }
        }

        foreach (var zone in await db.Zones.ToListAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var canon = Lookup(WorldCanonText.Zones, zone.Id);
            var key = $"zone:{zone.Id}";
            var before = changes.Count;

            Rewrite($"{key}.Name", zone.Name, canon?.Name, value => zone.Name = value, changes, unresolved);
            Rewrite($"{key}.Description", zone.Description, canon?.Description, value => zone.Description = value, changes, unresolved);

            if (changes.Count > before)
            {
                rows.Add(key);
                changedRows++;
            }
        }

        return changedRows > 0;
    }

    private static async Task<bool> SweepContentAsync(
        ContentDbContext db,
        List<string> changes,
        List<string> unresolved,
        HashSet<string> rows,
        CancellationToken cancellationToken)
    {
        var changedRows = 0;

        foreach (var map in await db.Maps.ToListAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var canon = Lookup(WorldCanonText.Maps, map.Id);
            var key = $"map:{map.Id}";
            var before = changes.Count;

            Rewrite($"{key}.Name", map.Name, canon?.Name, value => map.Name = value, changes, unresolved);
            Rewrite($"{key}.Description", map.Description, canon?.Description, value => map.Description = value, changes, unresolved);

            if (changes.Count > before)
            {
                rows.Add(key);
                changedRows++;
            }
        }

        foreach (var node in await db.MapNodes.ToListAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var canon = Lookup(WorldCanonText.MapNodes, node.Id);
            var key = $"mapnode:{node.Id}";
            var before = changes.Count;

            Rewrite($"{key}.DisplayName", node.DisplayName, canon?.Name, value => node.DisplayName = value, changes, unresolved);
            Rewrite($"{key}.Description", node.Description, canon?.Description, value => node.Description = value, changes, unresolved);

            if (changes.Count > before)
            {
                rows.Add(key);
                changedRows++;
            }
        }

        // The Maw's hidden location is labelled "Kaldrek's Heart" in a pre-rewrite database. Its slug is the key
        // and stays; only the label is rewritten.
        foreach (var location in await db.ZoneLocations.ToListAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var canon = Lookup(WorldCanonText.ZoneLocationDisplayNames, location.Slug);
            var key = $"zonelocation:{location.Slug}";
            var before = changes.Count;

            Rewrite($"{key}.DisplayName", location.DisplayName, canon, value => location.DisplayName = value, changes, unresolved);

            if (changes.Count > before)
            {
                rows.Add(key);
                changedRows++;
            }
        }

        return changedRows > 0;
    }

    private static TValue? Lookup<TValue>(IReadOnlyDictionary<string, TValue> canon, string id)
        where TValue : class
        => canon.TryGetValue(id, out var value) ? value : null;

    /// <summary>
    /// Rewrites <paramref name="current"/> when it still carries a retired token, records the field in
    /// <paramref name="changes"/>, and records it in <paramref name="unresolved"/> instead when there is no canon
    /// value to write.
    /// </summary>
    private static void Rewrite(
        string field,
        string? current,
        string? canonical,
        Action<string> assign,
        List<string> changes,
        List<string> unresolved)
    {
        if (!WorldCanonText.ContainsRetiredToken(current, out var token))
            return;

        if (canonical is null)
        {
            unresolved.Add($"{field} still carries \"{token}\" and has no canon entry");
            return;
        }

        assign(canonical);
        changes.Add(field);
    }
}
