using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Validation.Content;

/// <summary>
/// The set of slugs that actually exist in content, used by reference rules to prove a pointer resolves.
/// </summary>
/// <remarks>
/// Validators stay pure (entity in, issues out) and receive this catalog as an argument, which is what lets the
/// startup audit, the write endpoints and the CI guard all share one rule set without any of them owning a
/// <see cref="ContentDbContext"/>. Every set is case-insensitive and every lookup ignores blank entries, matching how
/// gameplay resolves content at runtime.
/// </remarks>
public sealed class ContentReferenceCatalog
{
    /// <summary>A catalog with no known content: every reference therefore reports as dangling.</summary>
    public static ContentReferenceCatalog Empty { get; } = new();

    /// <summary>Gets the actor-archetype slugs present in content.</summary>
    public IReadOnlySet<string> Archetypes { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the item slugs present in content (weapons, armour and consumables share one table).</summary>
    public IReadOnlySet<string> Items { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the material slugs present in content. Recipe ingredients and loot entries distinguish the two by domain
    /// (<c>items/materials</c> versus <c>items/general</c>), so both sets are needed to resolve them honestly.
    /// </summary>
    public IReadOnlySet<string> Materials { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the zone-location slugs present in content.</summary>
    public IReadOnlySet<string> Locations { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the power (ability/spell) slugs present in content.</summary>
    public IReadOnlySet<string> Powers { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the recipe slugs present in content.</summary>
    public IReadOnlySet<string> Recipes { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the loot-table slugs present in content.</summary>
    public IReadOnlySet<string> LootTables { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the skill slugs present in content.</summary>
    public IReadOnlySet<string> Skills { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the organisation slugs present in content.</summary>
    public IReadOnlySet<string> Organizations { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the dialogue slugs present in content.</summary>
    public IReadOnlySet<string> Dialogues { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the node ids present per map id, for map-edge checks.</summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> NodeIdsByMap { get; init; } =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Loads the catalog from the content catalog database.
    /// </summary>
    /// <param name="db">Content context to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A catalog describing every slug currently stored.</returns>
    public static async Task<ContentReferenceCatalog> LoadAsync(
        ContentDbContext db,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        var maps = await db.Maps.AsNoTracking()
            .Select(map => new
            {
                map.Id,
                NodeIds = map.Nodes.Select(node => node.Id).ToList(),
            })
            .ToListAsync(cancellationToken);

        return new ContentReferenceCatalog
        {
            Archetypes = Slugs(await db.ActorArchetypes.AsNoTracking().Select(a => a.Slug).ToListAsync(cancellationToken)),
            Items = Slugs(await db.Items.AsNoTracking().Select(i => i.Slug).ToListAsync(cancellationToken)),
            Materials = Slugs(await db.Materials.AsNoTracking().Select(m => m.Slug).ToListAsync(cancellationToken)),
            Locations = Slugs(await db.ZoneLocations.AsNoTracking().Select(l => l.Slug).ToListAsync(cancellationToken)),
            Powers = Slugs(await db.Powers.AsNoTracking().Select(p => p.Slug).ToListAsync(cancellationToken)),
            Recipes = Slugs(await db.Recipes.AsNoTracking().Select(r => r.Slug).ToListAsync(cancellationToken)),
            LootTables = Slugs(await db.LootTables.AsNoTracking().Select(t => t.Slug).ToListAsync(cancellationToken)),
            Skills = Slugs(await db.Skills.AsNoTracking().Select(s => s.Slug).ToListAsync(cancellationToken)),
            Organizations = Slugs(await db.Organizations.AsNoTracking().Select(o => o.Slug).ToListAsync(cancellationToken)),
            Dialogues = Slugs(await db.Dialogues.AsNoTracking().Select(d => d.Slug).ToListAsync(cancellationToken)),
            NodeIdsByMap = maps.ToDictionary(
                map => map.Id,
                map => (IReadOnlySet<string>)map.NodeIds.ToHashSet(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase),
        };
    }

    /// <summary>
    /// Resolves the catalog that a domain-qualified reference belongs to.
    /// </summary>
    /// <param name="domain">Domain string as stored on the row, e.g. <c>items/materials</c> or <c>items/general</c>.</param>
    /// <returns>The matching set, defaulting to <see cref="Items"/> when the domain is absent or unrecognised.</returns>
    public IReadOnlySet<string> ForDomain(string? domain) =>
        domain is not null && domain.Contains("materials", StringComparison.OrdinalIgnoreCase)
            ? Materials
            : Items;

    /// <summary>Builds a case-insensitive set from a slug sequence, dropping blanks and duplicates.</summary>
    /// <param name="slugs">Slugs to include.</param>
    /// <returns>The set.</returns>
    public static IReadOnlySet<string> Slugs(IEnumerable<string?> slugs) =>
        slugs.Where(slug => !string.IsNullOrWhiteSpace(slug))
            .Select(slug => slug!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
