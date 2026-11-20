using RealmEngine.Data.Content;

namespace RealmEngine.Data.Validation.Content;

/// <summary>
/// The catalog additions the content-pack pipeline needs: the reference sets for families that only GUID foreign keys
/// point at, and the ability to fold a pack's own slugs into the catalog before its entities are validated.
/// </summary>
public sealed partial class ContentReferenceCatalog
{
    /// <summary>Gets the species slugs present in content.</summary>
    public IReadOnlySet<string> Species { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the actor-class slugs present in content.</summary>
    public IReadOnlySet<string> ActorClasses { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the background slugs present in content.</summary>
    public IReadOnlySet<string> Backgrounds { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns a catalog that also knows the slugs a content pack declares.
    /// </summary>
    /// <remarks>
    /// A pack is validated as a whole, so an entity may reference a sibling that the same pack creates. Folding the
    /// declared slugs in before validating is what keeps a first-ever import of a self-contained pack from reporting
    /// every internal reference as dangling.
    /// </remarks>
    /// <param name="contentType">Content-type key the slugs belong to, e.g. <c>quest</c> or <c>weapon</c>.</param>
    /// <param name="slugs">The slugs to add.</param>
    /// <returns>
    /// A new catalog; this instance is unchanged. Content types no reference rule points at (instances, languages,
    /// material properties, enchantments) are accepted and ignored.
    /// </returns>
    public ContentReferenceCatalog WithContent(string contentType, IEnumerable<string> slugs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentNullException.ThrowIfNull(slugs);

        var added = Slugs(slugs);
        if (added.Count == 0)
        {
            return this;
        }

        var family = ContentEntityTypes.CanonicalFamily(contentType);
        var existing = SetFor(family);
        if (existing is null)
        {
            return this;
        }

        var merged = new HashSet<string>(existing.Concat(added), StringComparer.OrdinalIgnoreCase);
        return Replace(family, merged);
    }

    /// <summary>
    /// Gets the slug set a family resolves references against, when one exists.
    /// </summary>
    /// <param name="contentType">Content-type key, e.g. <c>loottable</c>.</param>
    /// <returns>The set, or <c>null</c> for families no reference rule points at.</returns>
    public IReadOnlySet<string>? SetFor(string contentType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        return ContentEntityTypes.CanonicalFamily(contentType) switch
        {
            "item" => Items,
            "ability" => Powers,
            "quest" => Quests,
            "recipe" => Recipes,
            "loottable" => LootTables,
            "zonelocation" => Locations,
            "archetype" => Archetypes,
            "dialogue" => Dialogues,
            "skill" => Skills,
            "organization" => Organizations,
            "material" => Materials,
            "species" => Species,
            "class" => ActorClasses,
            "background" => Backgrounds,
            _ => null,
        };
    }

    /// <summary>
    /// Copies the catalog with one set replaced. The sets are <c>init</c>-only, so this is the copy step.
    /// </summary>
    /// <param name="canonicalFamily">Canonical family key, already alias-folded.</param>
    /// <param name="value">The replacement set.</param>
    /// <returns>A new catalog.</returns>
    private ContentReferenceCatalog Replace(string canonicalFamily, IReadOnlySet<string> value) => new()
    {
        Archetypes = canonicalFamily == "archetype" ? value : Archetypes,
        Items = canonicalFamily == "item" ? value : Items,
        Materials = canonicalFamily == "material" ? value : Materials,
        Locations = canonicalFamily == "zonelocation" ? value : Locations,
        Powers = canonicalFamily == "ability" ? value : Powers,
        Recipes = canonicalFamily == "recipe" ? value : Recipes,
        LootTables = canonicalFamily == "loottable" ? value : LootTables,
        Skills = canonicalFamily == "skill" ? value : Skills,
        Organizations = canonicalFamily == "organization" ? value : Organizations,
        Dialogues = canonicalFamily == "dialogue" ? value : Dialogues,
        Quests = canonicalFamily == "quest" ? value : Quests,
        Species = canonicalFamily == "species" ? value : Species,
        ActorClasses = canonicalFamily == "class" ? value : ActorClasses,
        Backgrounds = canonicalFamily == "background" ? value : Backgrounds,
        NodeIdsByMap = NodeIdsByMap,
    };
}
