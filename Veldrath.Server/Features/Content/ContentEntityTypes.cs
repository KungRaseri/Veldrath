using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Validation.Content;
using DataQuest = RealmEngine.Data.Entities.Quest;

namespace Veldrath.Server.Features.Content;

/// <summary>
/// Resolves a content-type key to the entity type, the DbSet and the validator that handle it.
/// </summary>
/// <param name="ContentType">The canonical content-type key (what the validator is registered under).</param>
/// <param name="EntityType">The CLR type stored for this key.</param>
internal sealed record ContentEntityResolver(string ContentType, Type EntityType);

/// <summary>
/// The single place that knows how a content-type key maps onto the content catalog.
/// </summary>
/// <remarks>
/// The keys are the ones <c>ContentSchemaRegistry</c> advertises, which is also what the validator registry is keyed
/// by — so <c>GET /api/content/{type}/…</c> and <c>validate</c> agree on the vocabulary. Several keys deliberately
/// share one entity type (<c>item</c>/<c>weapon</c>/<c>armor</c>, <c>ability</c>/<c>spell</c>), exactly as the
/// validators do.
/// </remarks>
internal static class ContentEntityTypes
{
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

    /// <summary>Gets the content-type keys the write path accepts, in alphabetical order.</summary>
    internal static IReadOnlyList<string> KnownTypes => [.. _byTypeKey.Keys.Order(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Resolves a type key to its entity type and canonical validator key.</summary>
    /// <param name="typeKey">Content-type key from the URL.</param>
    /// <param name="resolver">The resolver, when the key is known.</param>
    /// <returns>True when the key is handled.</returns>
    internal static bool TryResolve(string typeKey, out ContentEntityResolver resolver)
    {
        if (typeKey is not null && _byTypeKey.TryGetValue(typeKey, out var entityType))
        {
            var registry = ContentValidatorRegistry.CreateDefault();
            var contentType = registry.TryGetValidator(typeKey, out _) ? typeKey : entityType.Name.ToLowerInvariant();
            resolver = new ContentEntityResolver(contentType, entityType);
            return true;
        }

        resolver = default!;
        return false;
    }

    /// <summary>Gets the validator registry the write path uses.</summary>
    internal static ContentValidatorRegistry Registry { get; } = ContentValidatorRegistry.CreateDefault();

    /// <summary>
    /// Finds the tracked entity with the given slug, so an update mutates the row instead of inserting a second one.
    /// </summary>
    /// <param name="db">Content context.</param>
    /// <param name="resolver">Resolved content type.</param>
    /// <param name="slug">Slug to look for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The entity, or null when it does not exist.</returns>
    internal static async Task<ContentBase?> FindBySlugAsync(
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
    /// <param name="payload">The request body.</param>
    /// <returns>The entity, or null when the body cannot be read as one.</returns>
    internal static ContentBase? Deserialize(ContentEntityResolver resolver, JsonElement payload)
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
    /// Finds the <see cref="DbSet{TEntity}"/> that stores <paramref name="entityType"/>.
    /// </summary>
    /// <remarks>
    /// EF Core's <c>Set<T>()</c> needs a compile-time type, and the write path only has a type key; the context
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

/// <summary>
/// Shapes the response the write path returns: the saved entity plus every issue the validator produced.
/// </summary>
internal static class ValidationResponse
{
    /// <summary>Builds the response body shared by <c>validate</c>, <c>POST</c> and <c>PUT</c>.</summary>
    /// <param name="entity">The entity that was validated or saved.</param>
    /// <param name="result">The validation result.</param>
    /// <returns>An anonymous object serialised as the response body.</returns>
    internal static object For(ContentBase entity, ContentValidationResult result) => new
    {
        entity,
        isValid = result.IsValid,
        errors = result.Errors,
        warnings = result.Warnings,
    };
}
