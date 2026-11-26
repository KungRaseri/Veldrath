using System.Text.Json;
using System.Text.Json.Nodes;
using RealmEngine.Data.Content;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Authoring.ContentPacks;

/// <summary>
/// What one import run did to one family.
/// </summary>
/// <param name="Family">Content-type key, e.g. <c>item</c>.</param>
/// <param name="Created">Rows inserted.</param>
/// <param name="Updated">Rows whose stored values changed.</param>
/// <param name="Unchanged">Rows that already matched the pack, and were not touched.</param>
public sealed record ContentPackFamilySummary(string Family, int Created, int Updated, int Unchanged)
{
    /// <summary>Gets the number of entities the pack declared for this family.</summary>
    public int Total => Created + Updated + Unchanged;
}

/// <summary>
/// The outcome of an import run.
/// </summary>
/// <param name="Validation">Every issue the pack produced, including the ones that blocked the write.</param>
/// <param name="Families">Per-family counts. Empty when validation failed.</param>
/// <param name="Written">False when validation failed or the run was a dry run.</param>
public sealed record ContentPackImportResult(
    ContentPackValidationResult Validation,
    IReadOnlyList<ContentPackFamilySummary> Families,
    bool Written)
{
    /// <summary>Gets a value indicating whether the pack passed validation (whether or not it was written).</summary>
    public bool Succeeded => Validation.IsValid;

    /// <summary>Gets the number of rows created.</summary>
    public int Created => Families.Sum(family => family.Created);

    /// <summary>Gets the number of rows updated.</summary>
    public int Updated => Families.Sum(family => family.Updated);

    /// <summary>Gets the number of rows left untouched.</summary>
    public int Unchanged => Families.Sum(family => family.Unchanged);
}

/// <summary>
/// Upserts a content pack into <see cref="ContentDbContext"/>, keyed by slug.
/// </summary>
/// <remarks>
/// The importer exists because the C# seeders are insert-only: they build a set of existing slugs and insert what is
/// missing, so editing a seeded record changes nothing on a database that already has it. An import creates the rows
/// a pack adds, updates the rows whose authored values changed, and leaves everything else alone — which is also what
/// makes a second import of the same pack report zero changes.
/// <para>
/// "Changed" is decided by comparing the entity as the database would return it with the entity as the pack describes
/// it, after stripping the three fields the server owns (<c>Id</c>, <c>Version</c>, <c>UpdatedAt</c>) and the ids of
/// nested rows. Comparing payloads rather than tracking EF state is what lets an unchanged entity cost one SELECT and
/// no UPDATE at all.
/// </para>
/// <para>
/// Validation runs first and nothing is written when it reports an <c>Error</c>: a pack is one unit, so half of it
/// landing in the catalog would be worse than none of it.
/// </para>
/// </remarks>
/// <param name="db">Content catalog to write. Never <c>ApplicationDbContext</c>.</param>
public sealed class ContentPackImporter(ContentDbContext db)
{
    /// <summary>
    /// Validates the pack and, when it is valid, upserts every entity in it by slug.
    /// </summary>
    /// <param name="read">The pack contents, as returned by <see cref="ContentPackReader.Read"/>.</param>
    /// <param name="dryRun">When true, work out the counts but do not save.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Per-family counts and every issue found.</returns>
    public async Task<ContentPackImportResult> ImportAsync(
        ContentPackReadResult read,
        bool dryRun = false,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(read);

        var validation = await new ContentPackValidator(db).ValidateAsync(read, ct);
        if (!validation.IsValid)
        {
            return new ContentPackImportResult(validation, [], Written: false);
        }

        var counts = new Dictionary<string, (int Created, int Updated, int Unchanged)>(StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;

        void Bump(string family, int created = 0, int updated = 0, int unchanged = 0)
        {
            var current = counts.GetValueOrDefault(family);
            counts[family] = (current.Created + created, current.Updated + updated, current.Unchanged + unchanged);
        }

        foreach (var file in read.Files)
        {
            if (!ContentEntityTypes.TryResolve(file.Family, out var resolver))
            {
                continue;
            }

            foreach (var entry in file.Entries)
            {
                var incoming = ContentEntityTypes.Deserialize(
                    resolver,
                    JsonSerializer.SerializeToElement(entry.Payload));

                if (incoming is null)
                {
                    // Validation already reported it; reaching here would mean the two passes disagree.
                    continue;
                }

                var existing = await ContentEntityTypes.FindBySlugAsync(db, resolver, incoming.Slug, ct);

                if (existing is null)
                {
                    if (!dryRun)
                    {
                        incoming.Id = incoming.Id == Guid.Empty ? Guid.NewGuid() : incoming.Id;
                        incoming.UpdatedAt = now;
                        db.Add(incoming);
                    }

                    Bump(file.Family, created: 1);

                    continue;
                }

                await ContentChildRows.LoadAsync(db, existing, ct);

                var stored = Canonical(existing).ToJsonString();
                var authored = Canonical(incoming).ToJsonString();

                if (stored == authored)
                {
                    Bump(file.Family, unchanged: 1);

                    continue;
                }


                if (!dryRun)
                {
                    CopyAuthoredValues(incoming, existing);
                    ContentChildRows.Reconcile(db, incoming, existing);
                    existing.Version += 1;
                    existing.UpdatedAt = now;
                }

                Bump(file.Family, updated: 1);
            }
        }

        if (!dryRun && counts.Values.Any(count => count.Created + count.Updated > 0))
        {
            await db.SaveChangesAsync(ct);
        }

        var families = counts
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new ContentPackFamilySummary(pair.Key, pair.Value.Created, pair.Value.Updated, pair.Value.Unchanged))
            .ToList();

        return new ContentPackImportResult(validation, families, Written: !dryRun);
    }

    /// <summary>
    /// Copies every authored property of <paramref name="incoming"/> onto the tracked row.
    /// </summary>
    /// <remarks>
    /// The copying itself lives in <see cref="ContentValueCopy.ApplyAuthoredValues"/> because the HTTP write endpoint and
    /// the review-queue approval path replace rows the same way and must not drift from the importer. Child rows that are
    /// entities of their own are not assigned there either: <see cref="ContentChildRows.Reconcile"/> matches them by
    /// identity, because assigning the authored list makes EF treat every child as an existing row it has to UPDATE.
    /// </remarks>
    private void CopyAuthoredValues(ContentBase incoming, ContentBase existing) =>
        ContentValueCopy.ApplyAuthoredValues(incoming, existing, db.Model);

    /// <summary>
    /// Projects an entity onto the fields a pack authors, and returns it as canonical JSON.
    /// </summary>
    /// <remarks>
    /// Serialising the tracked entity directly is not usable: EF has loaded the row's <c>Species</c> or
    /// <c>LootTable</c> navigation, so the JSON would contain a whole second entity that the pack never wrote. This
    /// projection walks the EF model instead — scalars, owned JSON values and authored child rows — which is also why
    /// the three server-owned columns and a child's foreign key to its parent never count as a difference.
    /// </remarks>
    /// <param name="entity">The entity to project.</param>
    /// <returns>Canonical JSON.</returns>
    private JsonObject Canonical(ContentBase entity)
    {
        var entityType = db.Model.FindEntityType(entity.GetType())
            ?? throw new InvalidOperationException($"'{entity.GetType().Name}' is not part of the content model.");

        var projected = ContentChildRows.Project(entityType, entity, parentType: null);
        var json = projected.ToJsonString();

        // Parsing again keeps the comparison stable regardless of how a value serialised the first time.
        return JsonNode.Parse(json) as JsonObject ?? projected;
    }
}
