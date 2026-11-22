using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Validation.Content;
using DataQuest = RealmEngine.Data.Entities.Quest;

namespace RealmEngine.Data.Content;

/// <summary>
/// Resolves a content-type key to the entity type and the validator that handle it.
/// </summary>
/// <param name="ContentType">The canonical content-type key (what the validator is registered under).</param>
/// <param name="EntityType">The CLR type stored for this key.</param>
public sealed record ContentEntityResolver(string ContentType, Type EntityType);

/// <summary>
/// The single place that knows how a content-type key maps onto the content catalog.
/// </summary>
/// <remarks>
/// The keys are the ones <c>ContentSchemaRegistry</c> advertises, which is also what the validator registry is keyed
/// by — so <c>GET /api/content/{type}/…</c>, <c>validate</c> and the content-pack importer all agree on the
/// vocabulary. Several keys deliberately share one entity type (<c>item</c>/<c>weapon</c>/<c>armor</c>,
/// <c>ability</c>/<c>spell</c>), exactly as the validators do.
/// <para>
/// This lives in the data library rather than in the server so the offline importer
/// (<c>Veldrath.ContentPack</c>) can resolve a family without loading ASP.NET Core, and so there is one map instead
/// of a server copy and a tool copy that drift.
/// </para>
/// </remarks>
public static class ContentEntityTypes
{
    /// <summary>JSON options for reading a content payload. Matches the JSON the read endpoints emit.</summary>
    private static readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<string, Type> _byTypeKey = new(StringComparer.OrdinalIgnoreCase)
    {
        ["item"] = typeof(Item),
        ["weapon"] = typeof(Item),
        ["armor"] = typeof(Item),
        ["ability"] = typeof(Power),
        ["spell"] = typeof(Power),
        ["power"] = typeof(Power),
        ["species"] = typeof(Species),
        ["class"] = typeof(ActorClass),
        ["archetype"] = typeof(ActorArchetype),
        ["instance"] = typeof(ActorInstance),
        ["background"] = typeof(Background),
        ["skill"] = typeof(Skill),
        ["material"] = typeof(Material),
        ["materialproperty"] = typeof(MaterialProperty),
        ["enchantment"] = typeof(Enchantment),
        ["quest"] = typeof(DataQuest),
        ["recipe"] = typeof(Recipe),
        ["loottable"] = typeof(LootTable),
        ["organization"] = typeof(Organization),
        ["zonelocation"] = typeof(ZoneLocation),
        ["dialogue"] = typeof(Dialogue),
        ["language"] = typeof(Language),
    };

    /// <summary>
    /// Gets the content-type keys the write path and the pack importer accept, in alphabetical order.
    /// A pack directory is named after one of these keys: <c>content/quests/…</c> uses <c>quest</c>.
    /// </summary>
    public static IReadOnlyList<string> KnownTypes => [.. _byTypeKey.Keys.Order(StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// Gets the content-type keys that share one stored entity type, e.g. <c>weapon</c> → <c>item</c>.
    /// </summary>
    /// <remarks>
    /// <c>power</c> is here for a different reason than the others: it is the key the read endpoints expose for the
    /// <c>Powers</c> table, while the write path and the schema advertise <c>ability</c>. Without the alias the write
    /// path handed the registry a key it has no validator for and answered 500 instead of validating — and a null child
    /// collection on that route therefore 500'd rather than being refused by name.
    /// </remarks>
    public static IReadOnlyDictionary<string, string> Aliases { get; } = new Dictionary<string, string>(
        StringComparer.OrdinalIgnoreCase)
    {
        ["weapon"] = "item",
        ["armor"] = "item",
        ["spell"] = "ability",
        ["power"] = "ability",
    };

    /// <summary>
    /// Folds a type-key alias onto the family that stores it.
    /// </summary>
    /// <param name="typeKey">Content-type key, e.g. <c>weapon</c>.</param>
    /// <returns>
    /// <c>item</c> for <c>weapon</c> and <c>armor</c>, <c>ability</c> for <c>spell</c> and <c>power</c>, the key itself
    /// otherwise.
    /// </returns>
    public static string CanonicalFamily(string typeKey) =>
        typeKey is not null && Aliases.TryGetValue(typeKey, out var canonical) ? canonical : typeKey ?? string.Empty;

    /// <summary>
    /// Validates an entity the way the write path must: the payload-shape rules first, then the family's own validator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the single entry point for "is this entity usable", so the HTTP endpoint, the <c>validate</c> dry run and
    /// the review-queue approval cannot disagree about a problem — the same reason they share <see cref="Registry"/>.
    /// Two rules need the EF model and the entity, not just one of them: a child-row or owned collection present as JSON
    /// <c>null</c> (<see cref="ContentChildRows.NullCollectionIssues"/>) and an owned-object reference present as
    /// <c>null</c> (<see cref="ContentChildRows.NullOwnedObjectIssues"/>). A family validator cannot see either — several
    /// families have no rule of their own that reads their collections — and leaving the collection case to the
    /// reconcile would delete the stored rows while leaving the object case to a validator is a null dereference.
    /// </para>
    /// <para>
    /// The shape issues are merged ahead of the family's so a caller reading the first error reads the real cause. A
    /// null owned object is the one shape that stops the family validator from running at all: the family rules that
    /// read it dereference it by design (<c>entity.Stats.MinLevel</c>), which is exactly the unhandled 500 the shape
    /// rule replaces, so the named issue is the whole answer.
    /// </para>
    /// </remarks>
    /// <param name="resolver">Resolved content type of <paramref name="entity"/>.</param>
    /// <param name="entity">The entity the payload describes.</param>
    /// <param name="model">Model of the context that will store the entity.</param>
    /// <param name="catalog">The slugs that exist, for reference checks.</param>
    /// <returns>Every issue, shape rules included.</returns>
    public static ContentValidationResult Validate(
        ContentEntityResolver resolver,
        ContentBase entity,
        IModel model,
        ContentReferenceCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(catalog);

        var shapeIssues = new List<ContentIssue>(ContentChildRows.NullCollectionIssues(entity, model));
        var nullOwnedObjects = ContentChildRows.NullOwnedObjectIssues(entity, model);
        shapeIssues.AddRange(nullOwnedObjects);

        // An owned object carried as null is not an entity a family validator can inspect; it dereferences it by
        // design. Report the shape issue by name and stop, rather than crashing before the caller can read it.
        if (nullOwnedObjects.Count > 0)
        {
            return new ContentValidationResult(shapeIssues);
        }

        var result = Registry.Validate(resolver.ContentType, entity, catalog);

        return shapeIssues.Count == 0
            ? result
            : new ContentValidationResult([.. shapeIssues, .. result.Issues]);
    }

    /// <summary>Gets the validator registry the write path and the importer use.</summary>
    public static ContentValidatorRegistry Registry { get; } = ContentValidatorRegistry.CreateDefault();

    /// <summary>Resolves a type key to its entity type and canonical validator key.</summary>
    /// <param name="typeKey">Content-type key, e.g. <c>item</c>.</param>
    /// <param name="resolver">The resolver, when the key is known.</param>
    /// <returns>True when the key is handled.</returns>
    public static bool TryResolve(string typeKey, out ContentEntityResolver resolver)
    {
        if (typeKey is not null && _byTypeKey.TryGetValue(typeKey, out var entityType))
        {
            // A key with a validator of its own keeps it (`weapon` stays `weapon`, not `item`). A key without one is a
            // read alias of the family that stores the row and must fold onto it — handing the registry a key it does
            // not know is a KeyNotFoundException, i.e. a 500 on a route that exists.
            var contentType = Registry.TryGetValidator(typeKey, out _)
                ? typeKey
                : CanonicalFamily(typeKey);

            resolver = new ContentEntityResolver(contentType, entityType);
            return true;
        }

        resolver = default!;
        return false;
    }

    /// <summary>
    /// Finds the tracked entity with the given slug, so an update mutates the row instead of inserting a second one.
    /// </summary>
    /// <param name="db">Content context.</param>
    /// <param name="resolver">Resolved content type.</param>
    /// <param name="slug">Slug to look for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The entity, or null when it does not exist.</returns>
    public static async Task<ContentBase?> FindBySlugAsync(
        ContentDbContext db,
        ContentEntityResolver resolver,
        string slug,
        CancellationToken ct)
    {
        var set = SetFor(db, resolver.EntityType);
        return set is null
            ? null
            : await set.FirstOrDefaultAsync(entity => entity.Slug == slug, ct);
    }

    /// <summary>
    /// Deserialises a JSON payload into the entity type for the resolved content type.
    /// </summary>
    /// <param name="resolver">Resolved content type.</param>
    /// <param name="payload">The request body, or a content-pack entity payload.</param>
    /// <returns>The entity, or null when the body cannot be read as one.</returns>
    public static ContentBase? Deserialize(ContentEntityResolver resolver, JsonElement payload)
    {
        try
        {
            return JsonSerializer.Deserialize(payload.GetRawText(), resolver.EntityType, _jsonOpts) as ContentBase;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Serialises a content entity back to the payload shape, so two entities can be compared honestly.
    /// </summary>
    /// <param name="entity">Entity to serialise.</param>
    /// <returns>Indented camelCase JSON.</returns>
    public static string Serialize(ContentBase entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return JsonSerializer.Serialize(entity, entity.GetType(), _jsonOpts);
    }

    /// <summary>Gets the JSON options used to read and write content payloads.</summary>
    public static JsonSerializerOptions JsonOptions => _jsonOpts;

    /// <summary>
    /// Finds the <see cref="DbSet{TEntity}"/> that stores <paramref name="entityType"/>.
    /// </summary>
    /// <remarks>
    /// EF Core's <c>Set&lt;T&gt;()</c> needs a compile-time type, and the write path only has a type key; the context
    /// already exposes one typed DbSet per entity, so that is the lookup used instead of a hand-maintained switch.
    /// </remarks>
    private static IQueryable<ContentBase>? SetFor(ContentDbContext db, Type entityType)
    {
        var property = typeof(ContentDbContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(candidate =>
                candidate.PropertyType.IsGenericType
                && candidate.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>)
                && candidate.PropertyType.GetGenericArguments()[0] == entityType);

        return property?.GetValue(db) as IQueryable<ContentBase>;
    }
}
