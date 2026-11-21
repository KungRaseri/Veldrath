using System.Text.Json;
using System.Text.Json.Nodes;
using RealmEngine.Data.Content;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Validation.Content;

namespace RealmEngine.Data.Authoring.ContentPacks;

/// <summary>
/// The issues a pack directory produced, and whether any of them blocks an import.
/// </summary>
/// <param name="Issues">File-level and entity-level issues together, in file order.</param>
public sealed record ContentPackValidationResult(IReadOnlyList<ContentIssue> Issues)
{
    /// <summary>A result with no issues.</summary>
    public static ContentPackValidationResult Empty { get; } = new([]);

    /// <summary>Gets a value indicating whether anything blocks the import.</summary>
    public bool IsValid => Issues.All(issue => issue.Severity != ContentIssueSeverity.Error);

    /// <summary>Gets the error-severity issues.</summary>
    public IReadOnlyList<ContentIssue> Errors =>
        [.. Issues.Where(issue => issue.Severity == ContentIssueSeverity.Error)];

    /// <summary>Gets the warning-severity issues.</summary>
    public IReadOnlyList<ContentIssue> Warnings =>
        [.. Issues.Where(issue => issue.Severity == ContentIssueSeverity.Warning)];
}

/// <summary>
/// Runs every entity in a pack through the shared content validators, without writing anything.
/// </summary>
/// <remarks>
/// This is deliberately a thin loop over <see cref="ContentEntityTypes.Registry"/> — the same registry the Foundry
/// write path calls — so a pack cannot be accepted by CI and then refused by the server, or the other way round. The
/// database is read (for the reference catalog and for resolving GUID foreign keys written as slugs) but never
/// written: <c>validate</c> against a live catalog is safe to run at any time.
/// </remarks>
/// <param name="db">Content catalog to resolve references against. Read-only.</param>
public sealed class ContentPackValidator(ContentDbContext db)
{
    /// <summary>
    /// GUID foreign keys that a pack may write as a slug, and the family the slug is resolved in.
    /// </summary>
    /// <remarks>
    /// The seeder resolves these ids by slug lookup (<c>ArchetypeSeeder</c> does exactly this for
    /// <c>LootTableId</c>), because no author can know a row's GUID. A pack writes the slug, the importer resolves it,
    /// and an unknown slug is an error rather than a silently empty column.
    /// </remarks>
    private static readonly Dictionary<(Type EntityType, string Property), string> _slugReferences = new()
    {
        [(typeof(ActorArchetype), "speciesId")] = "species",
        [(typeof(ActorArchetype), "classId")] = "class",
        [(typeof(ActorArchetype), "backgroundId")] = "background",
        [(typeof(ActorArchetype), "lootTableId")] = "loottable",
        [(typeof(ActorInstance), "archetypeId")] = "archetype",
        [(typeof(ActorInstance), "lootTableOverride")] = "loottable",
    };

    /// <summary>
    /// Validates every entity in <paramref name="read"/>.
    /// </summary>
    /// <param name="read">The pack contents, as returned by <see cref="ContentPackReader.Read"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Every issue found: layout, binding, slug-resolution and content rules.</returns>
    public async Task<ContentPackValidationResult> ValidateAsync(
        ContentPackReadResult read,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(read);

        var issues = new List<ContentIssue>(read.Issues);

        var catalog = await ContentReferenceCatalog.LoadAsync(db, ct);
        foreach (var file in read.Files)
        {
            // A file the reader could not attribute to a family carries no entities and already produced an issue
            // (pack-family-directory-required, pack-family-unknown or a file-level failure). Merging its family into
            // the catalog would hand it a blank or unknown content type, and the catalog rejects a blank one — which
            // turned one misplaced file into "validation could not run" with no path in the message.
            if (!ContentEntityTypes.TryResolve(file.Family, out _))
            {
                continue;
            }

            catalog = catalog.WithContent(file.Family, file.Entries.Select(entry => entry.Slug));
        }

        foreach (var file in read.Files)
        {
            if (!ContentEntityTypes.TryResolve(file.Family, out var resolver))
            {
                // The reader already reported why this file has no family; there is no entity type to bind.
                continue;
            }

            foreach (var entry in file.Entries)
            {
                await ResolveSlugReferencesAsync(entry, resolver, issues, ct);

                var entity = ContentEntityTypes.Deserialize(
                    resolver,
                    JsonSerializer.SerializeToElement(entry.Payload));

                if (entity is null)
                {
                    issues.Add(new ContentIssue(
                        ContentPackRules.EntityUnreadable,
                        $"{entry.Label} could not be read as a {file.Family}: {entry.SourcePath}#{entry.Position}. Check field names and value types against the content schema.",
                        ContentIssueSeverity.Error,
                        Field: file.Family,
                        Identifier: entry.Label));

                    continue;
                }

                issues.AddRange(ContentEntityTypes.Registry.Validate(file.Family, entity, catalog).Issues);
            }
        }

        return new ContentPackValidationResult(issues);
    }

    /// <summary>
    /// Replaces slug-valued GUID foreign keys with the id of the row they name.
    /// </summary>
    private async Task ResolveSlugReferencesAsync(
        ContentPackEntry entry,
        ContentEntityResolver resolver,
        List<ContentIssue> issues,
        CancellationToken ct)
    {
        foreach (var ((entityType, property), family) in _slugReferences)
        {
            if (entityType != resolver.EntityType || entry.Payload[property] is not JsonValue value)
            {
                continue;
            }

            if (!value.TryGetValue<string>(out var written) || string.IsNullOrWhiteSpace(written))
            {
                continue;
            }

            // A GUID is already an id; leave it exactly as written.
            if (Guid.TryParse(written, out _))
            {
                continue;
            }

            if (!ContentEntityTypes.TryResolve(family, out var target))
            {
                continue;
            }

            var referenced = await ContentEntityTypes.FindBySlugAsync(db, target, written, ct);
            if (referenced is null)
            {
                issues.Add(new ContentIssue(
                    ContentPackRules.ReferenceUnresolved,
                    $"{entry.Label} field '{property}' points at unknown {family} '{written}'. A GUID foreign key may be written as a slug, but that slug has to exist before the pack is imported.",
                    ContentIssueSeverity.Error,
                    Field: property,
                    Identifier: entry.Label));

                continue;
            }

            entry.Payload[property] = JsonValue.Create(referenced.Id);
        }
    }
}
