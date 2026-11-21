using System.Reflection;
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
