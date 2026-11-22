using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Validation.Content;

namespace RealmEngine.Data.Content;

/// <summary>
/// Reconciles the child rows an authored content entity owns with the rows already stored for it.
/// </summary>
/// <remarks>
/// <para>
/// A few content entities own children that are rows in their own table rather than owned JSON: a recipe's
/// <c>Ingredients</c> (<c>RecipeIngredient</c>, keyed <c>RecipeId+ItemDomain+ItemSlug</c>) and a loot table's
/// <c>Entries</c> (<c>LootTableEntry</c>, surrogate key). Assigning the authored collection is the one thing a write
/// path cannot do: a child deserialised from JSON arrives with its key already set, so EF classifies it as an existing
/// row and issues an UPDATE against a row that was never inserted — a <c>DbUpdateConcurrencyException</c> that aborts
/// the write. Mutating the tracked parent's collection instead is just as wrong, because the payload instance is not
/// the tracked instance.
/// </para>
/// <para>
/// So the children are matched by authored identity: matches are mutated in place, rows the payload dropped are deleted
/// and rows it added are inserted. An unchanged child costs nothing and an edit is one row.
/// </para>
/// <para>
/// This lives next to <see cref="ContentValueCopy"/> so the three paths that replace a content row — the HTTP write
/// endpoint, the review-queue approval and the content-pack importer — share one implementation instead of three that
/// drift. The importer adds the change detection on top; the match/mutate/insert/delete half is this class.
/// </para>
/// <para>
/// <b>Deletion is driven by presence, not by emptiness.</b> A caller passes the names of the collections the submitted
/// payload actually names (see <see cref="ApplyNamedAsync"/>). A collection the payload never mentions is left exactly
/// as stored: a schema-shaped submission that does not render recipe ingredients would otherwise delete every stored
/// ingredient for an entity it never showed. A collection the payload names is replaced wholesale, so an empty list in
/// the payload still clears it — that is the full-replace contract for anything the caller actually wrote. The generic
/// detail read names every collection by loading it (<see cref="IncludeAuthoredChildren"/>) precisely so an editor that
/// starts from that payload is never in the position of naming a collection it was not shown.
/// </para>
/// <para>
/// <b>An explicit <c>null</c> is neither of those.</b> Omission means "leave the stored rows alone" and an empty array
/// means "clear them"; a property present as JSON <c>null</c> says nothing about the author's intent, so it is refused
/// with a named issue (<see cref="NullCollectionIssues"/>, rule <c>child_collection_null</c>) instead of being read as an
/// empty authored set. Two guards make that unambiguous: <see cref="NamedIn"/> only counts a property that carries a
/// JSON array as naming a collection, and <see cref="Reconcile"/> skips a null collection even when the caller asked for
/// every collection (the pack importer does). A null therefore can never delete rows, on any path.
/// </para>
/// </remarks>
public static class ContentChildRows
{
    /// <summary>
    /// Gets whether a navigation is a collection of child rows this entity owns, as opposed to owned JSON or a
    /// navigation to another content entity.
    /// </summary>
    /// <remarks>
    /// <c>Archetype.Instances</c> is the inverse of <c>ActorInstance.ArchetypeId</c>, so it is the other side of a child
    /// that points at this row rather than something this row authored; <c>Recipe.Ingredients</c> is not.
    /// </remarks>
    /// <param name="navigation">Navigation to classify.</param>
    /// <returns>True when the navigation holds child rows.</returns>
    public static bool IsAuthoredCollection(INavigationBase navigation) =>
        navigation.IsCollection
        && !typeof(ContentBase).IsAssignableFrom(navigation.TargetEntityType.ClrType);

    /// <summary>
    /// Loads every child-row collection of a tracked entity, so a comparison or a reconcile can see the stored rows.
    /// </summary>
    /// <remarks>
    /// Without this the tracked row reports "no children" for every stored entity — which makes the importer rewrite
    /// every parent on every import, and makes a reconcile insert a second copy of every child.
    /// </remarks>
    /// <param name="db">Context tracking <paramref name="existing"/>.</param>
    /// <param name="existing">The tracked row whose children to load.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the collections are loaded.</returns>
    public static async Task LoadAsync(DbContext db, ContentBase existing, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(existing);

        var entry = db.Entry(existing);

        foreach (var navigation in entry.Navigations)
        {
            if (IsAuthoredCollection(navigation.Metadata) && !navigation.Metadata.TargetEntityType.IsOwned())
            {
                await entry.Collection(navigation.Metadata.Name).LoadAsync(ct);
            }
        }
    }

    /// <summary>
    /// Includes every authored child-row collection, so a read path returns the rows a write path reconciles.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The generic detail read feeds both editors: Foundry's form starts from it, and the review queue seeds a
    /// submission from it. A detail that hides the child rows is not merely incomplete — it is destructive. The editor
    /// sends back the payload it loaded, so a collection serialised as <c>[]</c> is named as an empty list, and a named
    /// collection is replaced wholesale (<see cref="ApplyNamedAsync"/>). Hiding the ingredients once meant the first
    /// save deleted them.
    /// </para>
    /// <para>
    /// The navigations are enumerated from the EF model rather than listed by hand, so a new child table is covered by
    /// both the read path and <see cref="Reconcile"/> or by neither.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The content entity being read.</typeparam>
    /// <param name="query">Query to extend.</param>
    /// <param name="model">Model that maps <typeparamref name="T"/>.</param>
    /// <returns>The query with every authored child-row collection included.</returns>
    public static IQueryable<T> IncludeAuthoredChildren<T>(this IQueryable<T> query, IModel model)
        where T : ContentBase
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(model);

        if (model.FindEntityType(typeof(T)) is not { } entityType)
        {
            return query;
        }

        foreach (var navigation in entityType.GetNavigations())
        {
            if (IsAuthoredCollection(navigation)
                && !navigation.TargetEntityType.IsOwned()
                && navigation.PropertyInfo is { CanWrite: true })
            {
                query = query.Include(navigation.Name);
            }
        }

        return query;
    }

    /// <summary>
    /// Gets the names of the child-row collections a submitted payload names.
    /// </summary>
    /// <remarks>
    /// The deserialiser accepts either casing (<see cref="JsonSerializerDefaults.Web"/> is case-insensitive), so the
    /// lookup is case-insensitive too: a payload that names <c>ingredients</c> and one that names <c>Ingredients</c>
    /// both mean the author wrote the collection.
    /// <para>
    /// Only a property that carries a JSON array names a collection. A property present as <c>null</c> (or as a scalar)
    /// is not an authored set, and treating it as one would reconcile the stored rows against nothing — a silent delete.
    /// The write path refuses such a payload before it gets here (<see cref="NullCollectionIssues"/>).
    /// </para>
    /// </remarks>
    /// <param name="entityType">CLR type of the entity the payload describes.</param>
    /// <param name="payload">The submitted payload.</param>
    /// <param name="model">Model of the context that will store the entity.</param>
    /// <returns>The collection property names the payload names; empty when it names none.</returns>
    public static IReadOnlySet<string> NamedIn(Type entityType, JsonElement payload, IModel model)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(model);

        var names = new HashSet<string>(StringComparer.Ordinal);

        if (payload.ValueKind != JsonValueKind.Object || model.FindEntityType(entityType) is not { } mapped)
        {
            return names;
        }

        foreach (var navigation in mapped.GetNavigations())
        {
            if (IsAuthoredCollection(navigation)
                && !navigation.TargetEntityType.IsOwned()
                && navigation.PropertyInfo is { CanWrite: true }
                && NamesCollection(payload, navigation.Name))
            {
                names.Add(navigation.Name);
            }
        }

        return names;
    }

    /// <summary>
    /// Reports every authored child-row collection <paramref name="entity"/> carries as <c>null</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An omitted property deserialises to the collection's own initializer (<c>[]</c>), so a null here means the payload
    /// named the collection and gave it no value. That is a shape error, not an authoring choice: it cannot be read as
    /// "clear the rows" (the payload never said how many) and it must not be read as "leave them alone" silently either,
    /// because an editor that sends it is broken and would otherwise believe it saved. The rule names the field so the
    /// author can fix it — omit the property to keep the stored rows, send <c>[]</c> to clear them.
    /// </para>
    /// <para>
    /// It lives here, driven by the EF model, because the authored collections are exactly what this class reconciles:
    /// several families (species, class, archetype, instance, power) have no rule of their own that reads those
    /// collections, so a per-validator check would leave their nulls reaching the reconcile.
    /// </para>
    /// </remarks>
    /// <param name="entity">The entity the payload describes.</param>
    /// <param name="model">Model of the context that will store the entity.</param>
    /// <returns>One error per null child-row collection; empty when every collection has a value.</returns>
    public static IReadOnlyList<ContentIssue> NullCollectionIssues(ContentBase entity, IModel model)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(model);

        if (model.FindEntityType(entity.GetType()) is not { } entityType)
        {
            return [];
        }

        var identifier = string.IsNullOrWhiteSpace(entity.Slug) ? null : entity.Slug;
        var issues = new List<ContentIssue>();

        foreach (var navigation in entityType.GetNavigations())
        {
            if (!IsAuthoredCollection(navigation)
                || navigation.TargetEntityType.IsOwned()
                || navigation.PropertyInfo is not { CanRead: true } property
                || property.GetValue(entity) is not null)
            {
                continue;
            }

            var field = JsonNamingPolicy.CamelCase.ConvertName(navigation.Name);

            issues.Add(new ContentIssue(
                ContentRules.ChildCollectionNull,
                $"'{entity.GetType().Name}' '{identifier ?? "(no slug)"}' names '{field}' as null. An explicit null is not "
                + "an empty collection: omit the property to leave the stored children alone, or send an empty array to "
                + "replace them with nothing.",
                ContentIssueSeverity.Error,
                field,
                identifier));
        }

        return issues;
    }

    /// <summary>
    /// Loads and reconciles exactly the child-row collections <paramref name="payload"/> names.
    /// </summary>
    /// <remarks>
    /// This is the HTTP write path's entry point. The payload-presence rule documented on this class is what makes an
    /// update that omits a collection a no-op for that collection instead of a silent delete.
    /// </remarks>
    /// <param name="db">Context tracking both entities.</param>
    /// <param name="incoming">The entity the payload describes.</param>
    /// <param name="existing">The tracked row it replaces, which is mutated in place.</param>
    /// <param name="payload">The submitted payload, used to decide which collections the author wrote.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the authored children have been reconciled.</returns>
    public static async Task ApplyNamedAsync(
        DbContext db,
        ContentBase incoming,
        ContentBase existing,
        JsonElement payload,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(existing);

        var names = NamedIn(existing.GetType(), payload, db.Model);
        if (names.Count == 0)
        {
            return;
        }

        var entry = db.Entry(existing);

        foreach (var name in names)
        {
            await entry.Collection(name).LoadAsync(ct);
        }

        Reconcile(db, incoming, existing, names);
    }

    /// <summary>
    /// Reconciles child rows by authored identity: matches are mutated, dropped rows are deleted and additions are
    /// inserted.
    /// </summary>
    /// <param name="db">Context tracking <paramref name="existing"/>.</param>
    /// <param name="incoming">The entity as the editor, the approval or the pack authored it.</param>
    /// <param name="existing">The tracked row, which is mutated in place.</param>
    /// <param name="names">
    /// Collections to reconcile, or <c>null</c> for every child-row collection the entity type has. The collections must
    /// already be loaded on <paramref name="existing"/>.
    /// </param>
    public static void Reconcile(
        DbContext db,
        ContentBase incoming,
        ContentBase existing,
        IReadOnlySet<string>? names = null)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(existing);

        var entityType = db.Model.FindEntityType(existing.GetType());
        if (entityType is null)
        {
            return;
        }

        var entry = db.Entry(existing);

        foreach (var navigation in entityType.GetNavigations())
        {
            if (!IsAuthoredCollection(navigation)
                || navigation.TargetEntityType.IsOwned()
                || navigation.PropertyInfo is not { CanWrite: true } property)
            {
                continue;
            }

            if (names is not null && !names.Contains(navigation.Name))
            {
                continue;
            }

            if (!entry.Collection(navigation.Name).IsLoaded)
            {
                throw new InvalidOperationException(
                    $"'{existing.GetType().Name}.{navigation.Name}' must be loaded before its children are reconciled; "
                    + "reconciling an unloaded collection would insert a second copy of every stored child.");
            }

            var childType = navigation.TargetEntityType;
            var authoredValue = property.GetValue(incoming);

            // A null collection is not an authored set. The caller may have asked for every collection (the pack
            // importer does), and there is no payload to consult, so the safe reading is the one the API documents:
            // omit means "leave the stored rows alone". The HTTP write path refuses this shape before it gets here.
            if (authoredValue is null)
            {
                continue;
            }

            var authored = ToObjects(authoredValue);
            var stored = ToObjects(property.GetValue(existing));
            var identity = ChildIdentityParts(childType, entityType);

            var removed = identity.Count > 0
                ? ReconcileByKey(db, childType, entityType, identity, authored, stored, existing)
                : ReconcileByValues(db, childType, entityType, authored, stored, existing);

            // EF marks the rows deleted but leaves them in the parent's loaded collection, while it does add the
            // inserted children to it. Without this the tracked parent reports a collection that is neither the old
            // set nor the authored one — and the write response is serialised from that parent.
            Detach(property.GetValue(existing), removed);
        }
    }

    /// <summary>
    /// Projects an entity onto the fields its author writes, as JSON.
    /// </summary>
    /// <remarks>
    /// Serialising a tracked entity directly is not usable: EF has loaded its navigations, so the JSON would contain a
    /// whole second entity the author never wrote. This walks the EF model instead — scalars, owned JSON values and
    /// authored child rows — which is also why the server-owned columns and a child's foreign key to its parent never
    /// count as a difference between two entities.
    /// </remarks>
    /// <param name="type">Entity type to project.</param>
    /// <param name="instance">Instance to project.</param>
    /// <param name="parentType">The owning entity type when projecting a child row, otherwise <c>null</c>.</param>
    /// <returns>The authored surface, as JSON.</returns>
    public static JsonObject Project(IEntityType type, object instance, IEntityType? parentType)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(instance);

        var projected = new JsonObject();

        foreach (var property in type.GetProperties())
        {
            if (property.IsPrimaryKey() || property.PropertyInfo is null)
            {
                continue;
            }

            // Server-owned columns. Nobody authors them, so they must not count as a difference.
            if (parentType is null && property.Name is nameof(ContentBase.Version) or nameof(ContentBase.UpdatedAt))
            {
                continue;
            }

            if (parentType is not null
                && property.IsForeignKey()
                && property.GetContainingForeignKeys().Any(key => key.PrincipalEntityType == parentType))
            {
                // The child's link to its parent is assigned by EF, not authored.
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

    /// <summary>
    /// Gets the child-key properties the author writes, or an empty list when the child has no authorable identity.
    /// </summary>
    /// <remarks>
    /// A key part that is a foreign key to the parent is assigned by the write path, and a generated part is a surrogate
    /// the author cannot know, so neither can identify a child. What remains is identity the author wrote: an
    /// ingredient's item domain and slug.
    /// </remarks>
    private static IReadOnlyList<IProperty> ChildIdentityParts(IEntityType childType, IEntityType parentType) =>
        childType.FindPrimaryKey() is { } key
            ? [.. key.Properties.Where(property =>
                property.ValueGenerated == ValueGenerated.Never
                && !property.GetContainingForeignKeys().Any(
                    foreignKey => foreignKey.PrincipalEntityType == parentType))]
            : [];

    /// <summary>Matches stored child rows on the identity the author wrote, mutating matches and replacing the rest.</summary>
    /// <param name="db">Context that will insert and delete rows.</param>
    /// <param name="childType">Child entity type.</param>
    /// <param name="parentType">The owning entity type.</param>
    /// <param name="identity">Key parts that identify a child.</param>
    /// <param name="authored">The children as the caller describes them.</param>
    /// <param name="stored">The tracked children.</param>
    /// <param name="parent">The tracked parent row.</param>
    /// <returns>The stored rows to delete.</returns>
    private static List<object> ReconcileByKey(
        DbContext db,
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
                AddChild(db, childType, parentType, child, parent);
                continue;
            }

            unmatched.Remove(match);
            ApplyChildValues(childType, parentType, child, match);
        }

        db.RemoveRange(unmatched);

        return unmatched;
    }

    /// <summary>Matches stored child rows on their authored values, for a child whose key is a generated surrogate.</summary>
    /// <param name="db">Context that will insert and delete rows.</param>
    /// <param name="childType">Child entity type.</param>
    /// <param name="parentType">The owning entity type.</param>
    /// <param name="authored">The children as the caller describes them.</param>
    /// <param name="stored">The tracked children.</param>
    /// <param name="parent">The tracked parent row.</param>
    /// <returns>The stored rows to delete.</returns>
    private static List<object> ReconcileByValues(
        DbContext db,
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
                AddChild(db, childType, parentType, child, parent);
                continue;
            }

            unmatched.Remove(match);
            ApplyChildValues(childType, parentType, child, match);
        }

        db.RemoveRange(unmatched);

        return unmatched;
    }

    /// <summary>Copies a matched child's authored values onto the tracked child row.</summary>
    /// <param name="childType">Child entity type.</param>
    /// <param name="parentType">The owning entity type, so the child's FK to it is left alone.</param>
    /// <param name="authored">The child as the caller describes it.</param>
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
    /// <param name="db">Context that will insert the row.</param>
    /// <param name="childType">Child entity type.</param>
    /// <param name="parentType">The owning entity type.</param>
    /// <param name="child">The child as the caller describes it.</param>
    /// <param name="parent">The tracked parent row.</param>
    /// <remarks>
    /// <para>
    /// <c>db.Add</c> rather than a graph discovered through the navigation: an added entity is an INSERT regardless of
    /// the key value the authored child arrived with, which is exactly what the assignment path got wrong.
    /// </para>
    /// <para>
    /// A generated key is cleared first. The payload a caller sends back may carry the surrogate id of the row it was
    /// read from, and a replaced row is deleted and re-inserted in the same save: reusing that id would insert a row
    /// whose key the same transaction is deleting, which the database rejects. The id is not authored, so the insert
    /// takes a fresh one.
    /// </para>
    /// </remarks>
    private static void AddChild(DbContext db, IEntityType childType, IEntityType parentType, object child, ContentBase parent)
    {
        foreach (var key in childType.FindPrimaryKey()?.Properties ?? [])
        {
            if (key.ValueGenerated != ValueGenerated.Never
                && key.PropertyInfo is { CanWrite: true } keyInfo
                && !key.GetContainingForeignKeys().Any(foreignKey => foreignKey.PrincipalEntityType == parentType))
            {
                keyInfo.SetValue(child, key.ClrType.IsValueType ? Activator.CreateInstance(key.ClrType) : null);
            }
        }

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

    /// <summary>Removes the deleted rows from the parent's loaded collection, so it reports the authored set.</summary>
    /// <param name="collection">The parent's collection value.</param>
    /// <param name="removed">The rows that were deleted.</param>
    private static void Detach(object? collection, IReadOnlyList<object> removed)
    {
        if (collection is null || removed.Count == 0)
        {
            return;
        }

        foreach (var child in removed)
        {
            if (collection is IList list)
            {
                list.Remove(child);
                continue;
            }

            collection.GetType()
                .GetMethod("Remove", BindingFlags.Public | BindingFlags.Instance, [child.GetType()])
                ?.Invoke(collection, [child]);
        }
    }

    /// <summary>Materialises a navigation value as a list of non-null children.</summary>
    /// <param name="value">The navigation value, or null.</param>
    /// <returns>The children, or an empty list.</returns>
    private static List<object> ToObjects(object? value) =>
        value is IEnumerable sequence and not string
            ? [.. sequence.Cast<object?>().Where(child => child is not null).Select(child => child!)]
            : [];

    /// <summary>
    /// Gets whether the payload names <paramref name="name"/> with a JSON array, which is the only shape that describes an
    /// authored collection.
    /// </summary>
    /// <param name="payload">Payload to read.</param>
    /// <param name="name">Collection property name to look for, in any casing.</param>
    /// <returns>True when the property is present and carries an array.</returns>
    private static bool NamesCollection(JsonElement payload, string name)
    {
        foreach (var property in payload.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.ValueKind == JsonValueKind.Array;
            }
        }

        return false;
    }

    private static JsonArray ProjectMany(INavigation navigation, object value, IEntityType parentType)
    {
        var array = new JsonArray();

        foreach (var child in (IEnumerable)value)
        {
            if (child is not null)
            {
                array.Add(Project(navigation.TargetEntityType, child, parentType));
            }
        }

        return array;
    }
}
