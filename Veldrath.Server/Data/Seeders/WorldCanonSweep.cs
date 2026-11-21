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
/// <param name="DriftedFields">
/// Canon-covered fields that differ from canon without a retired token or a recorded pre-rewrite value, quoting the
/// value found. They are left exactly as they are: on <c>Worlds</c>/<c>Regions</c>/<c>Zones</c> — which only
/// <see cref="ApplicationDataSeeder"/> writes — this means a shipped text change nobody recorded in
/// <see cref="WorldCanonText.KnownStaleValues"/>; on the map graph it is content authored in Foundry and is not
/// reported at all.
/// </param>
public sealed record WorldCanonSweepResult(
    int RowsChanged,
    int FieldsChanged,
    IReadOnlyList<string> ChangedFields,
    IReadOnlyList<string> UnresolvedFields,
    IReadOnlyList<string> DriftedFields);

/// <summary>
/// Rewrites world-geography text that still carries retired lore, so a database seeded before the canon rewrite
/// is corrected on the next start instead of keeping the old prose forever.
/// </summary>
/// <remarks>
/// <para>
/// Both world seeders stop as soon as their tables have rows — <see cref="ApplicationDataSeeder"/> and
/// <c>MapGraphSeeder</c> — so the seed text only ever reaches an empty database. This sweep is the path for an
/// already-seeded one, and it is guarded rather than an upsert: a field is rewritten only when its current value
/// still contains one of <see cref="WorldCanonText.RetiredTokens"/>, matched case-sensitively, or is exactly a
/// recorded pre-rewrite value from <see cref="WorldCanonText.KnownStaleValues"/>. Anything an author
/// edited later — including map nodes saved through the Foundry map editor, which writes the same rows — matches
/// neither and is never touched. The token match cannot catch a reword that drops the old name without adding
/// anything, so the exact-match set and the drift report exist for that case: a reword with no token is swept when
/// its old text is recorded, and reported when it is not.
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
        var drifted = new List<string>();
        var rows = new HashSet<string>(StringComparer.Ordinal);

        // Row-level prose in the application database has one writer — ApplicationDataSeeder — so a value that
        // differs from canon is a text change this sweep was not told how to recognise, and it is reported. The map
        // graph is written by the Foundry editor as well, so divergence there is authored content and stays quiet.
        var applicationChanged = await SweepApplicationDataAsync(applicationDb, changes, unresolved, drifted, rows, cancellationToken);
        if (applicationChanged)
            await applicationDb.SaveChangesAsync(cancellationToken);

        var contentChanged = await SweepContentAsync(contentDb, changes, unresolved, rows, cancellationToken);
        if (contentChanged)
            await contentDb.SaveChangesAsync(cancellationToken);

        return new WorldCanonSweepResult(
            rows.Count,
            changes.Count,
            changes.AsReadOnly(),
            unresolved.AsReadOnly(),
            drifted.AsReadOnly());
    }

    private static async Task<bool> SweepApplicationDataAsync(
        ApplicationDbContext db,
        List<string> changes,
        List<string> unresolved,
        List<string> drifted,
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

            Rewrite($"{key}.Name", world.Name, canon?.Name, value => world.Name = value, changes, unresolved, drifted);
            Rewrite($"{key}.Description", world.Description, canon?.Description, value => world.Description = value, changes, unresolved, drifted);
            Rewrite($"{key}.Era", world.Era, canon?.Era, value => world.Era = value, changes, unresolved, drifted);

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

            Rewrite($"{key}.Name", region.Name, canon?.Name, value => region.Name = value, changes, unresolved, drifted);
            Rewrite($"{key}.Description", region.Description, canon?.Description, value => region.Description = value, changes, unresolved, drifted);

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

            Rewrite($"{key}.Name", zone.Name, canon?.Name, value => zone.Name = value, changes, unresolved, drifted);
            Rewrite($"{key}.Description", zone.Description, canon?.Description, value => zone.Description = value, changes, unresolved, drifted);

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

            Rewrite($"{key}.Name", map.Name, canon?.Name, value => map.Name = value, changes, unresolved, null);
            Rewrite($"{key}.Description", map.Description, canon?.Description, value => map.Description = value, changes, unresolved, null);

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

            Rewrite($"{key}.DisplayName", node.DisplayName, canon?.Name, value => node.DisplayName = value, changes, unresolved, null);
            Rewrite($"{key}.Description", node.Description, canon?.Description, value => node.Description = value, changes, unresolved, null);

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

            Rewrite($"{key}.DisplayName", location.DisplayName, canon, value => location.DisplayName = value, changes, unresolved, null);

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
    /// Brings <paramref name="current"/> back to <paramref name="canonical"/> when it is recognisably stale, records
    /// the field in <paramref name="changes"/>, and reports the cases it will not touch.
    /// </summary>
    /// <remarks>
    /// A value is stale when it still carries a retired token or is exactly a recorded pre-rewrite value
    /// (<see cref="WorldCanonText.KnownStaleValues"/>) — the latter is how a reword that carries no token, such as the
    /// world description, still reaches a migrated database. Anything else is compared against canon only: when
    /// <paramref name="drifted"/> is supplied the difference is recorded there and the text is left alone, never
    /// overwritten; the map graph passes <see langword="null"/> because the Foundry editor writes those rows.
    /// With no canon entry the field cannot be repaired, so it is reported in <paramref name="unresolved"/> instead.
    /// </remarks>
    private static void Rewrite(
        string field,
        string? current,
        string? canonical,
        Action<string> assign,
        List<string> changes,
        List<string> unresolved,
        List<string>? drifted)
    {
        if (canonical is null)
        {
            if (WorldCanonText.ContainsRetiredToken(current, out var orphanToken))
                unresolved.Add($"{field} still carries \"{orphanToken}\" and has no canon entry");

            return;
        }

        if (string.Equals(current, canonical, StringComparison.Ordinal))
            return;

        if (WorldCanonText.ContainsRetiredToken(current, out _)
            || (current is not null && WorldCanonText.KnownStaleValues.Contains(current)))
        {
            assign(canonical);
            changes.Add(field);
            return;
        }

        drifted?.Add($"{field} differs from the shipped canon and holds no retired token: \"{current}\"");
    }
}
