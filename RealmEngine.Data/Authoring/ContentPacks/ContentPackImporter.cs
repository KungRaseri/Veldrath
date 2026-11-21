using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
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

                await LoadChildrenAsync(existing, ct);

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
                    ReconcileChildren(incoming, existing);
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
    /// Loads the child rows of a tracked entity, so the comparison sees them too.
    /// </summary>
    /// <remarks>
    /// A recipe's ingredients and a loot table's entries are real rows, not owned JSON. Without loading them the
    /// comparison would see "no children" for every stored entity and rewrite it on every import. Inverse navigations
    /// (<c>Archetype.Instances</c>) are not loaded: they are the other side of a child that points at this row, so
    /// they are not part of what the pack authored.
    /// </remarks>
    private async Task LoadChildrenAsync(ContentBase existing, CancellationToken ct)
    {
        var entry = db.Entry(existing);

        foreach (var navigation in entry.Navigations)
        {
            if (IsAuthoredCollection(navigation.Metadata))
            {
                await entry.Collection(navigation.Metadata.Name).LoadAsync(ct);
            }
        }
    }

    /// <summary>
    /// Copies every authored property of <paramref name="incoming"/> onto the tracked row.
    /// </summary>
    /// <remarks>
    /// The copying itself lives in <see cref="ContentValueCopy.ApplyAuthoredValues"/> because the HTTP write endpoint and
    /// the review-queue approval path replace rows the same way and must not drift from the importer. Child rows that are
    /// entities of their own are not assigned there either: <see cref="ReconcileChildren"/> matches them by identity,
    /// because assigning the authored list makes EF treat every child as an existing row it has to UPDATE.
    /// </remarks>
    private void CopyAuthoredValues(ContentBase incoming, ContentBase existing) =>
        ContentValueCopy.ApplyAuthoredValues(incoming, existing, db.Model);

    /// <summary>
    /// Reconciles the child rows a pack authors with the rows already stored, instead of handing the tracked parent a
    /// collection of instances EF has never seen.
    /// </summary>
    /// <remarks>
    /// Assigning the authored collection is the one thing this cannot do. A child deserialised from JSON arrives with
    /// its generated key already set (<c>LootTableEntry.Id = Guid.NewGuid()</c>), so EF classifies it as an existing
    /// row and issues an UPDATE for a row that was never inserted — a <c>DbUpdateConcurrencyException</c> that aborts
    /// the whole import. Reconciling instead matches the pack against the stored children, mutates the matches in
    /// place, deletes the rows the pack dropped and inserts the ones it added, so an unchanged child costs nothing and
    /// an edit is one row.
    /// </remarks>
    private void ReconcileChildren(ContentBase incoming, ContentBase existing)
    {
        var entityType = db.Model.FindEntityType(existing.GetType());
        if (entityType is null)
        {
            return;
        }

        foreach (var navigation in entityType.GetNavigations())
        {
            if (!IsAuthoredCollection(navigation)
                || navigation.TargetEntityType.IsOwned()
                || navigation.PropertyInfo is not { CanWrite: true } property)
            {
                continue;
            }

            var childType = navigation.TargetEntityType;
            var authored = ToObjects(property.GetValue(incoming));
            var stored = ToObjects(property.GetValue(existing));
            var identity = ChildIdentityParts(childType, entityType);

            if (identity.Count > 0)
            {
                ReconcileByKey(childType, entityType, identity, authored, stored, existing);
            }
            else
            {
                ReconcileByValues(childType, entityType, authored, stored, existing);
            }
        }
    }

    /// <summary>
    /// Gets the child-key properties the pack itself authors, or an empty list when the child has no authorable identity.
    /// </summary>
    /// <remarks>
    /// A key part that is a foreign key to the parent is assigned by the importer, and a generated part is a surrogate
    /// the pack cannot know, so neither can identify a child. What remains is identity the author wrote: an ingredient's
    /// item, a map node's id.
    /// </remarks>
    private static IReadOnlyList<IProperty> ChildIdentityParts(IEntityType childType, IEntityType parentType) =>
        childType.FindPrimaryKey() is { } key
            ? [.. key.Properties.Where(property =>
                property.ValueGenerated == ValueGenerated.Never
                && !property.GetContainingForeignKeys().Any(
                    foreignKey => foreignKey.PrincipalEntityType == parentType))]
            : [];

    /// <summary>Matches stored child rows on the identity the pack authored, mutating matches and replacing the rest.</summary>
    private void ReconcileByKey(
        IEntityType childType,
        IEntityType parentType,
        IReadOnlyList<IProperty> identity,
        IReadOnlyList<object> authored,
        IReadOnlyList<object> stored,
        ContentBase parent)
    {
        var unmatched = new List<object>(stored);

        foreach (var child in authored)
        {
            var match = unmatched.FirstOrDefault(candidate => identity.All(part =>
                Equals(part.PropertyInfo?.GetValue(candidate), part.PropertyInfo?.GetValue(child))));

            if (match is null)
            {
                AddChild(childType, parentType, child, parent);
                continue;
            }

            unmatched.Remove(match);
            ApplyChildValues(childType, parentType, child, match);
        }

        db.RemoveRange(unmatched);
    }

    /// <summary>Matches stored child rows on their authored values, for a child whose key is a generated surrogate.</summary>
    private void ReconcileByValues(
        IEntityType childType,
        IEntityType parentType,
        IReadOnlyList<object> authored,
        IReadOnlyList<object> stored,
        ContentBase parent)
    {
        var unmatched = new List<object>(stored);

        foreach (var child in authored)
        {
            // The projection is the same one the change comparison uses: authored columns only, so a surrogate key or
            // the parent's foreign key can never make two rows look different (or the same) by accident.
            var values = Project(childType, child, parentType).ToJsonString();
            var match = unmatched.FirstOrDefault(candidate =>
                Project(childType, candidate, parentType).ToJsonString() == values);

            if (match is null)
            {
                AddChild(childType, parentType, child, parent);
                continue;
            }

            unmatched.Remove(match);
            ApplyChildValues(childType, parentType, child, match);
        }

        db.RemoveRange(unmatched);
    }

    /// <summary>Copies a matched child's authored values onto the tracked child row.</summary>
    /// <param name="childType">Child entity type.</param>
    /// <param name="parentType">The owning entity type, so the child's FK to it is left alone.</param>
    /// <param name="authored">The child as the pack describes it.</param>
    /// <param name="stored">The tracked child row.</param>
    private static void ApplyChildValues(IEntityType childType, IEntityType parentType, object authored, object stored)
    {
        var references = childType.GetNavigations()
            .Select(navigation => navigation.PropertyInfo)
            .OfType<PropertyInfo>()
            .ToHashSet();

        foreach (var property in childType.GetProperties())
        {
            if (property.IsPrimaryKey()
                || property.PropertyInfo is not { CanWrite: true } info
                || references.Contains(info)
                || (property.IsForeignKey() && property.GetContainingForeignKeys().Any(
                    foreignKey => foreignKey.PrincipalEntityType == parentType)))
            {
                continue;
            }

            info.SetValue(stored, info.GetValue(authored));
        }

        foreach (var navigation in childType.GetNavigations())
        {
            if (navigation.IsCollection
                || !navigation.TargetEntityType.IsOwned()
                || navigation.PropertyInfo is not { CanWrite: true } owned
                || owned.GetValue(authored) is not { } value)
            {
                continue;
            }

            if (owned.GetValue(stored) is { } current)
            {
                ContentValueCopy.CopyOwnedInPlace(value, current);
            }
        }
    }

    /// <summary>Inserts an authored child row, with its foreign keys and parent navigation set explicitly.</summary>
    /// <param name="childType">Child entity type.</param>
    /// <param name="parentType">The owning entity type.</param>
    /// <param name="child">The child as the pack describes it.</param>
    /// <param name="parent">The tracked parent row.</param>
    /// <remarks>
    /// <c>db.Add</c> rather than a graph discovered through the navigation: an added entity is an INSERT regardless of
    /// the key value the authored child arrived with, which is exactly what the assignment path got wrong.
    /// </remarks>
    private void AddChild(IEntityType childType, IEntityType parentType, object child, ContentBase parent)
    {
        foreach (var foreignKey in childType.GetForeignKeys().Where(
            foreignKey => foreignKey.PrincipalEntityType == parentType))
        {
            for (var index = 0; index < foreignKey.Properties.Count; index++)
            {
                var target = foreignKey.Properties[index].PropertyInfo;
                var source = foreignKey.PrincipalKey.Properties[index].PropertyInfo;

                if (target is { CanWrite: true } && source is not null)
                {
                    target.SetValue(child, source.GetValue(parent));
                }
            }
        }

        foreach (var navigation in childType.GetNavigations())
        {
            if (!navigation.IsCollection
                && navigation.ForeignKey.PrincipalEntityType == parentType
                && navigation.PropertyInfo is { CanWrite: true } reference)
            {
                reference.SetValue(child, parent);
            }
        }

        db.Add(child);
    }

    /// <summary>Materialises a navigation value as a list of non-null children.</summary>
    /// <param name="value">The navigation value, or null.</param>
    /// <returns>The children, or an empty list.</returns>
    private static List<object> ToObjects(object? value) =>
        value is System.Collections.IEnumerable sequence and not string
            ? [.. sequence.Cast<object?>().Where(child => child is not null).Select(child => child!)]
            : [];

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

        var projected = Project(entityType, entity, parentType: null);
        var json = projected.ToJsonString();

        // Parsing again keeps the comparison stable regardless of how a value serialised the first time.
        return JsonNode.Parse(json) as JsonObject ?? projected;
    }

    private static JsonObject Project(IEntityType type, object instance, IEntityType? parentType)
    {
        var projected = new JsonObject();

        foreach (var property in type.GetProperties())
        {
            if (property.IsPrimaryKey() || property.PropertyInfo is null)
            {
                continue;
            }

            // Server-owned columns. A pack never writes them, so they must not count as a difference.
            if (parentType is null && property.Name is nameof(ContentBase.Version) or nameof(ContentBase.UpdatedAt))
            {
                continue;
            }

            if (parentType is not null
                && property.IsForeignKey()
                && property.GetContainingForeignKeys().Any(key => key.PrincipalEntityType == parentType))
            {
                // The child's link to its parent is assigned by EF, not authored by the pack.
                continue;
            }

            projected[property.Name] = JsonSerializer.SerializeToNode(
                property.PropertyInfo.GetValue(instance),
                property.ClrType);
        }

        foreach (var navigation in type.GetNavigations())
        {
            if (navigation.PropertyInfo?.GetValue(instance) is not { } value)
            {
                continue;
            }

            if (navigation.TargetEntityType.IsOwned())
            {
                projected[navigation.Name] = navigation.IsCollection
                    ? ProjectMany(navigation, value, type)
                    : Project(navigation.TargetEntityType, value, type);

                continue;
            }

            if (IsAuthoredCollection(navigation))
            {
                projected[navigation.Name] = ProjectMany(navigation, value, type);
            }
        }

        return projected;
    }

    private static JsonArray ProjectMany(INavigation navigation, object value, IEntityType parentType)
    {
        var array = new JsonArray();

        foreach (var child in (System.Collections.IEnumerable)value)
        {
            if (child is not null)
            {
                array.Add(Project(navigation.TargetEntityType, child, parentType));
            }
        }

        return array;
    }

    /// <summary>
    /// A collection the pack authors: child rows owned by this row, as opposed to navigations that point at another
    /// content entity (<c>Archetype.Instances</c> is the inverse of <c>ActorInstance.ArchetypeId</c>).
    /// </summary>
    private static bool IsAuthoredCollection(INavigationBase navigation) =>
        navigation.IsCollection
        && !typeof(ContentBase).IsAssignableFrom(navigation.TargetEntityType.ClrType);
}
