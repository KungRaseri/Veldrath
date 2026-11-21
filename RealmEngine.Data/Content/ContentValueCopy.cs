using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using RealmEngine.Data.Entities;

namespace RealmEngine.Data.Content;

/// <summary>
/// Copies an authored content entity onto the tracked row it replaces.
/// </summary>
/// <remarks>
/// <para>
/// Every write path that replaces an existing content row needs this, because
/// <c>Entry(existing).CurrentValues.SetValues(incoming)</c> only walks the owner's scalar columns. Owned JSON types
/// (<c>Item.Stats</c>, quest objectives, a language's <c>Phonology</c>/<c>Morphology</c>/<c>RegisterSystem</c>) are
/// separate entries in EF's change tracker, so <c>SetValues</c> leaves them at their stored values while the write
/// reports success and bumps the version.
/// </para>
/// <para>
/// This lives here so the HTTP write endpoint, the review-queue approval service and the content-pack importer cannot
/// drift apart: they all replace rows, and each needed the same correction.
/// </para>
/// </remarks>
public static class ContentValueCopy
{
    /// <summary>
    /// Copies every authored scalar and owned-JSON value of <paramref name="incoming"/> onto the tracked row
    /// <paramref name="existing"/>.
    /// </summary>
    /// <remarks>
    /// The caller owns the server-side columns: <c>Id</c>, <c>Version</c> and <c>UpdatedAt</c> are never copied, so the
    /// row keeps its identity and its version is bumped exactly once, by the caller.
    /// <para>
    /// Navigations to other content entities are skipped in both directions. A reference is fixed up by EF from the
    /// foreign key scalar that is copied, and assigning the authored instance would hand the tracker a row it has never
    /// seen; a child-row collection (recipe ingredients, loot-table entries) has to be reconciled by identity instead,
    /// because the deserialised children arrive with generated keys EF would treat as existing rows.
    /// </para>
    /// </remarks>
    /// <param name="incoming">The entity authored by the editor or the pack.</param>
    /// <param name="existing">The row EF is tracking, which is mutated in place.</param>
    /// <param name="model">The model of the context tracking <paramref name="existing"/>.</param>
    public static void ApplyAuthoredValues(ContentBase incoming, ContentBase existing, IModel model)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(model);

        var entityType = model.FindEntityType(existing.GetType());

        var navigations = entityType is null
            ? []
            : entityType.GetNavigations()
                .Where(navigation => !navigation.TargetEntityType.IsOwned())
                .Select(navigation => navigation.PropertyInfo?.Name)
                .OfType<string>()
                .ToHashSet(StringComparer.Ordinal);

        var ownedReferences = entityType is null
            ? []
            : entityType.GetNavigations()
                .Where(navigation => navigation is { IsCollection: false } && navigation.TargetEntityType.IsOwned())
                .Select(navigation => navigation.PropertyInfo?.Name)
                .OfType<string>()
                .ToHashSet(StringComparer.Ordinal);

        foreach (var property in existing.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanWrite
                || navigations.Contains(property.Name)
                || property.Name is nameof(ContentBase.Id) or nameof(ContentBase.Version) or nameof(ContentBase.UpdatedAt))
            {
                continue;
            }

            var value = property.GetValue(incoming);

            if (ownedReferences.Contains(property.Name)
                && property.GetValue(existing) is { } current
                && value is not null)
            {
                // An owned JSON value has to be updated in place: replacing the instance leaves EF comparing the old,
                // still-tracked one, and the column keeps its stored values.
                CopyOwnedInPlace(value, current);
                continue;
            }

            property.SetValue(existing, value);
        }
    }

    /// <summary>
    /// Fills the owned-JSON values a payload does not name from the row it is about to replace.
    /// </summary>
    /// <remarks>
    /// A submission authored through a schema-shaped form names only the fields the schema exposes. The language
    /// schema, for instance, exposes four of the seven <c>phonology</c> lists and neither <c>morphology</c> nor
    /// <c>registerSystem</c>; deserialising that payload produces empty collections for everything it omitted, and
    /// applying them in place would delete what the author never saw. This copies the unnamed values from the stored
    /// row first, so the write only replaces what the payload names — an empty list in the payload still clears its
    /// stored counterpart, which keeps the full-replace contract for anything the caller actually wrote.
    /// </remarks>
    /// <param name="existing">The tracked row the payload would replace.</param>
    /// <param name="payload">The authored payload.</param>
    /// <param name="model">The model of the context tracking <paramref name="existing"/>.</param>
    /// <returns>The payload, with the owned-JSON values it did not name filled in.</returns>
    public static JsonElement FillUnnamedOwnedValues(ContentBase existing, JsonElement payload, IModel model)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(model);

        if (payload.ValueKind != JsonValueKind.Object)
        {
            return payload;
        }

        var entityType = model.FindEntityType(existing.GetType());
        if (entityType is null)
        {
            return payload;
        }

        if (JsonNode.Parse(payload.GetRawText()) is not JsonObject authored
            || JsonSerializer.SerializeToNode(existing, existing.GetType(), ContentEntityTypes.JsonOptions)
                is not JsonObject stored)
        {
            return payload;
        }

        foreach (var navigation in entityType.GetNavigations())
        {
            if (!navigation.TargetEntityType.IsOwned() || navigation.PropertyInfo?.Name is not { } propertyName)
            {
                continue;
            }

            var jsonName = JsonNamingPolicy.CamelCase.ConvertName(propertyName);

            if (!TryGetProperty(authored, jsonName, out var authoredValue))
            {
                // The payload never mentions the owned value at all, so the whole stored subtree is kept.
                if (TryGetProperty(stored, jsonName, out var storedValue))
                {
                    authored[jsonName] = storedValue?.DeepClone();
                }

                continue;
            }

            // The payload names the object but may leave some of its leaves out; only those are filled.
            if (authoredValue is JsonObject authoredObject
                && TryGetProperty(stored, jsonName, out var storedNode)
                && storedNode is JsonObject storedObject)
            {
                FillUnnamed(authoredObject, storedObject);
            }
        }

        return JsonSerializer.SerializeToElement(authored, ContentEntityTypes.JsonOptions);
    }

    /// <summary>Recursively fills the properties an authored owned-JSON object leaves out.</summary>
    /// <param name="authored">The object as the payload names it.</param>
    /// <param name="stored">The same object as the stored row carries it.</param>
    private static void FillUnnamed(JsonObject authored, JsonObject stored)
    {
        foreach (var (name, storedValue) in stored)
        {
            if (!TryGetProperty(authored, name, out var authoredValue))
            {
                authored[name] = storedValue?.DeepClone();
                continue;
            }

            if (authoredValue is JsonObject authoredObject && storedValue is JsonObject storedObject)
            {
                FillUnnamed(authoredObject, storedObject);
            }
        }
    }

    /// <summary>
    /// Reads a property by name, falling back to a case-insensitive match.
    /// </summary>
    /// <remarks>
    /// The deserialiser accepts either casing (<see cref="JsonSerializerDefaults.Web"/> is case-insensitive), so the
    /// fill has to as well — looking up only the camelCase spelling would add a second key for a PascalCase payload and
    /// the stored value would then win over the authored one.
    /// </remarks>
    /// <param name="source">Object to read from.</param>
    /// <param name="name">Property name to look for.</param>
    /// <param name="value">The property value, or <c>null</c> when absent.</param>
    /// <returns>True when the property is present in either casing.</returns>
    private static bool TryGetProperty(JsonObject source, string name, out JsonNode? value)
    {
        if (source.TryGetPropertyValue(name, out value))
        {
            return true;
        }

        foreach (var (key, candidate) in source)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                value = candidate;
                return true;
            }
        }

        value = null;
        return false;
    }

    /// <summary>Copies the writable properties of one owned value onto another, recursing into nested owned values.</summary>
    /// <remarks>
    /// An owned collection is assigned rather than merged: the authored list is what the editor or pack wrote, so a
    /// dropped element has to disappear. The nested reference case recurses because those instances are tracked too.
    /// </remarks>
    /// <param name="source">Value authored by the editor or the pack.</param>
    /// <param name="target">Value EF is tracking.</param>
    public static void CopyOwnedInPlace(object source, object target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        foreach (var property in target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanWrite)
            {
                continue;
            }

            var value = property.GetValue(source);
            var current = property.GetValue(target);

            var isNestedOwned = property.PropertyType.IsClass
                && property.PropertyType != typeof(string)
                && !typeof(System.Collections.IEnumerable).IsAssignableFrom(property.PropertyType);

            if (isNestedOwned && value is not null && current is not null)
            {
                CopyOwnedInPlace(value, current);
                continue;
            }

            property.SetValue(target, value);
        }
    }
}
