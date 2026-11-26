using System.Text.Json;
using RealmEngine.Data.Content;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Validation.Content;

namespace Veldrath.Server.Features.Content;

/// <summary>The outcome of writing one content entity: its slug, or the reason it was refused.</summary>
/// <param name="Slug">Slug of the entity that was written, or <c>null</c> when nothing was.</param>
/// <param name="Error">Why the write was refused, or <c>null</c> on success.</param>
public sealed record ContentPromotion(string? Slug, string? Error)
{
    /// <summary>An entity was written.</summary>
    /// <param name="slug">Its slug.</param>
    /// <returns>The outcome.</returns>
    public static ContentPromotion Succeeded(string slug) => new(slug, null);

    /// <summary>Nothing was written.</summary>
    /// <param name="error">The reason, worded for a reviewer.</param>
    /// <returns>The outcome.</returns>
    public static ContentPromotion Refused(string error) => new(null, error);
}

/// <summary>
/// Writes one entity onto the content catalog from its JSON payload.
/// </summary>
/// <remarks>
/// This exists so Foundry's review gate can land an approved submission without going through HTTP. It uses the
/// same three primitives the editor's write endpoint uses — <see cref="ContentEntityTypes"/> for the shape,
/// <see cref="ContentReferenceCatalog"/> for cross-references and <see cref="ContentEntityTypes.Registry"/> for
/// the rules — so a payload that the editor would refuse is refused here for the same reason. Only the
/// orchestration is repeated; the rules are not.
/// </remarks>
public class ContentWriteService(ContentDbContext db)
{
    /// <summary>
    /// Writes the entity described by <paramref name="payloadJson"/>: creates it, or replaces the stored row
    /// that carries the same slug.
    /// </summary>
    /// <remarks>
    /// The review queue's diff tells a reviewer that approving "would replace the stored entry", so approval has
    /// to be able to do exactly that. It used to call a create-only path, which refused every submission whose
    /// slug already existed — a community could propose a correction and no reviewer could ever accept it.
    /// Replacing keeps the row's id (live game data points at it) and bumps its version, the same way the editor's
    /// update endpoint does.
    /// </remarks>
    /// <param name="type">Content type key, e.g. <c>item</c>.</param>
    /// <param name="payloadJson">The stored payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The slug written, or the reason it was not.</returns>
    public async Task<ContentPromotion> PromoteAsync(
        string type,
        string payloadJson,
        CancellationToken ct = default)
    {
        if (!ContentEntityTypes.TryResolve(type, out var resolver))
        {
            return ContentPromotion.Refused($"Unknown content type '{type}'.");
        }

        JsonElement payload;
        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            payload = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return ContentPromotion.Refused("The submission payload is not valid JSON.");
        }

        var incoming = ContentEntityTypes.Deserialize(resolver, payload);
        if (incoming is null)
        {
            return ContentPromotion.Refused($"The payload does not describe a {type}.");
        }

        if (string.IsNullOrWhiteSpace(incoming.Slug))
        {
            return ContentPromotion.Refused("The payload has no slug, so there is nothing to address it by.");
        }

        var existing = await ContentEntityTypes.FindBySlugAsync(db, resolver, incoming.Slug, ct);

        if (existing is not null)
        {
            // A submission payload is authored through a schema-shaped form, so it names only the fields the schema
            // exposes. Fill the owned-JSON values it never mentions from the row being replaced before validation and
            // application; otherwise approving a one-field correction would delete everything the form could not show.
            payload = ContentValueCopy.FillUnnamedOwnedValues(existing, payload, db.Model);
            incoming = ContentEntityTypes.Deserialize(resolver, payload);
            if (incoming is null)
            {
                return ContentPromotion.Refused($"The payload does not describe a {type}.");
            }
        }

        var catalog = await ContentReferenceCatalog.LoadAsync(db, ct);
        var result = ContentEntityTypes.Validate(resolver, incoming, db.Model, catalog);
        if (!result.IsValid)
        {
            var reasons = result.Errors.Select(issue => issue.Message).Take(3);

            return ContentPromotion.Refused(
                $"The payload does not pass the content rules: {string.Join(" ", reasons)}"
            );
        }

        if (existing is null)
        {
            incoming.Id = incoming.Id == Guid.Empty ? Guid.NewGuid() : incoming.Id;
            incoming.UpdatedAt = DateTimeOffset.UtcNow;
            db.Add(incoming);
        }
        else
        {
            // Id survives (foreign keys point at it); the server owns Version and UpdatedAt, so they are stamped on the
            // tracked row rather than copied over with the rest of the authored values.
            existing.Version += 1;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            ContentValueCopy.ApplyAuthoredValues(incoming, existing, db.Model);

            // Child rows are reconciled by authored identity, not copied; a submission that never names a collection
            // leaves the stored rows alone rather than deleting what the form could not show.
            await ContentChildRows.ApplyNamedAsync(db, incoming, existing, payload, ct);
        }

        await db.SaveChangesAsync(ct);

        return ContentPromotion.Succeeded(incoming.Slug);
    }
}
