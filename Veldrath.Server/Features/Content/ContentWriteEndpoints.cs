using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Validation.Content;
using Veldrath.Server.Features.Auth;

namespace Veldrath.Server.Features.Content;

/// <summary>
/// The content write path used by Foundry, the only content editor.
/// </summary>
/// <remarks>
/// <para>
/// Reads on <c>/api/content</c> stay anonymous; every write requires the <c>ManageContent</c> permission (the
/// <c>ContentEditor</c> and <c>Admin</c> roles carry it). Writes go through <see cref="ContentDbContext"/> only — the
/// gameplay contexts are never touched.
/// </para>
/// <para>
/// The <c>validate</c> dry run and the real write call the same <see cref="ContentValidatorRegistry"/>, so they can
/// never disagree: an entity that validates clean is exactly an entity that saves.
/// </para>
/// <para>
/// There is no hard delete. Rows are deactivated (<c>IsActive = false</c>) because live game rows reference content
/// by slug and a hard delete would turn a reference into a crash instead of a reported issue.
/// </para>
/// </remarks>
public static class ContentWriteEndpoints
{
    private static readonly JsonSerializerOptions _writeJsonOpts = new(JsonSerializerDefaults.Web);

    /// <summary>Maps the content write endpoints.</summary>
    /// <param name="app">Endpoint route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapContentWriteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/content")
            .WithTags("Content (write)")
            .RequireAuthorization(Permissions.ManageContent);

        group.MapGet("/{type}/{slug}/validate", ValidateAsync)
            .WithSummary("Dry-run validation for the stored content entity.");
        group.MapPost("/{type}/{slug}/validate", ValidatePayloadAsync)
            .WithSummary("Dry-run validation for an edited payload that has not been saved.");
        group.MapPost("/{type}", CreateAsync)
            .WithSummary("Create a content entity after validating it.");
        group.MapPut("/{type}/{slug}", UpdateAsync)
            .WithSummary("Update a content entity after validating it; the slug is immutable.");
        group.MapDelete("/{type}/{slug}", DeactivateAsync)
            .WithSummary("Deactivate a content entity (soft delete).");

        return app;
    }

    /// <summary>
    /// Validates the entity stored at <paramref name="type"/>/<paramref name="slug"/> and returns its issues without
    /// persisting anything.
    /// </summary>
    private static async Task<IResult> ValidateAsync(
        string type,
        string slug,
        ContentDbContext db,
        CancellationToken ct)
    {
        if (!ContentEntityTypes.TryResolve(type, out var resolver))
        {
            return UnknownType(type);
        }

        var entity = await ContentEntityTypes.FindBySlugAsync(db, resolver, slug, ct);
        if (entity is null)
        {
            return Results.NotFound(new { error = $"No {type} with slug '{slug}'." });
        }

        var catalog = await ContentReferenceCatalog.LoadAsync(db, ct);
        var result = ContentEntityTypes.Registry.Validate(resolver.ContentType, entity, catalog);
        return Results.Ok(ValidationResponse.For(entity, result));
    }

    /// <summary>
    /// Validates a payload the editor has not saved yet, so <em>Validate</em> can check unsaved edits instead of only
    /// re-reporting whatever is already stored.
    /// </summary>
    private static async Task<IResult> ValidatePayloadAsync(
        string type,
        string slug,
        JsonElement payload,
        ContentDbContext db,
        CancellationToken ct)
    {
        if (!ContentEntityTypes.TryResolve(type, out var resolver))
        {
            return UnknownType(type);
        }

        var incoming = ContentEntityTypes.Deserialize(resolver, payload);
        if (incoming is null)
        {
            return Results.BadRequest(new { error = "The request body is not a valid content payload." });
        }

        // The route owns the slug: validating a rename would report issues for an entity that cannot exist.
        incoming.Slug = slug;

        var catalog = await ContentReferenceCatalog.LoadAsync(db, ct);
        var result = ContentEntityTypes.Registry.Validate(resolver.ContentType, incoming, catalog);
        return Results.Ok(ValidationResponse.For(incoming, result));
    }

    /// <summary>Creates an entity of <paramref name="type"/>, rejecting any request that has error-level issues.</summary>
    private static async Task<IResult> CreateAsync(
        string type,
        JsonElement payload,
        ContentDbContext db,
        CancellationToken ct)
    {
        if (!ContentEntityTypes.TryResolve(type, out var resolver))
        {
            return UnknownType(type);
        }

        var incoming = ContentEntityTypes.Deserialize(resolver, payload);
        if (incoming is null)
        {
            return Results.BadRequest(new { error = "The request body is not a valid content payload." });
        }

        if (string.IsNullOrWhiteSpace(incoming.Slug))
        {
            return Results.BadRequest(new { error = "A slug is required." });
        }

        var existing = await ContentEntityTypes.FindBySlugAsync(db, resolver, incoming.Slug, ct);
        if (existing is not null)
        {
            return Results.Conflict(new { error = $"A {type} with slug '{incoming.Slug}' already exists." });
        }

        var catalog = await ContentReferenceCatalog.LoadAsync(db, ct);
        var result = ContentEntityTypes.Registry.Validate(resolver.ContentType, incoming, catalog);
        if (!result.IsValid)
        {
            return Results.BadRequest(ValidationResponse.For(incoming, result));
        }

        incoming.Id = incoming.Id == Guid.Empty ? Guid.NewGuid() : incoming.Id;
        incoming.UpdatedAt = DateTimeOffset.UtcNow;
        db.Add(incoming);
        await db.SaveChangesAsync(ct);

        return Results.Created(
            $"/api/content/browse/{type}/{incoming.Slug}",
            ValidationResponse.For(incoming, result));
    }

    /// <summary>
    /// Replaces the writable fields of the entity at <paramref name="slug"/>.
    /// </summary>
    /// <param name="type">Content type key.</param>
    /// <param name="slug">Slug of the entity to update; immutable.</param>
    /// <param name="expectedUpdatedAt">
    /// Optional <c>UpdatedAt</c> the editor loaded. When supplied and stale, the update is refused with <c>409</c> so a
    /// blind overwrite of someone else's edit is impossible.
    /// </param>
    /// <param name="payload">The new entity.</param>
    /// <param name="db">Content context.</param>
    /// <param name="ct">Cancellation token.</param>
    private static async Task<IResult> UpdateAsync(
        string type,
        string slug,
        DateTimeOffset? expectedUpdatedAt,
        JsonElement payload,
        ContentDbContext db,
        CancellationToken ct)
    {
        if (!ContentEntityTypes.TryResolve(type, out var resolver))
        {
            return UnknownType(type);
        }

        var existing = await ContentEntityTypes.FindBySlugAsync(db, resolver, slug, ct);
        if (existing is null)
        {
            return Results.NotFound(new { error = $"No {type} with slug '{slug}'." });
        }

        if (expectedUpdatedAt is not null && existing.UpdatedAt.ToUniversalTime() != expectedUpdatedAt.Value.ToUniversalTime())
        {
            return Results.Conflict(new
            {
                error = "This entity changed since it was loaded.",
                expectedUpdatedAt = expectedUpdatedAt.Value.ToString("O", CultureInfo.InvariantCulture),
                actualUpdatedAt = existing.UpdatedAt.ToString("O", CultureInfo.InvariantCulture),
            });
        }

        var incoming = ContentEntityTypes.Deserialize(resolver, payload);
        if (incoming is null)
        {
            return Results.BadRequest(new { error = "The request body is not a valid content payload." });
        }

        if (!string.Equals(incoming.Slug, slug, StringComparison.Ordinal))
        {
            return Results.BadRequest(new { error = "The slug is immutable; send the same slug or create a new entity." });
        }

        var catalog = await ContentReferenceCatalog.LoadAsync(db, ct);
        var result = ContentEntityTypes.Registry.Validate(resolver.ContentType, incoming, catalog);
        if (!result.IsValid)
        {
            return Results.BadRequest(ValidationResponse.For(incoming, result));
        }

        // Copy the writable surface onto the tracked row. Id must survive (FK rows point at it) and UpdatedAt/Version
        // are owned by the server, so they are stamped on the incoming copy before the copy happens.
        incoming.Id = existing.Id;
        incoming.Version = existing.Version + 1;
        incoming.UpdatedAt = DateTimeOffset.UtcNow;
        db.Entry(existing).CurrentValues.SetValues(incoming);
        await db.SaveChangesAsync(ct);

        return Results.Ok(ValidationResponse.For(existing, result));
    }

    /// <summary>Deactivates one entity. There is no hard delete: content rows are referenced by live game data.</summary>
    private static async Task<IResult> DeactivateAsync(
        string type,
        string slug,
        ContentDbContext db,
        CancellationToken ct)
    {
        if (!ContentEntityTypes.TryResolve(type, out var resolver))
        {
            return UnknownType(type);
        }

        var existing = await ContentEntityTypes.FindBySlugAsync(db, resolver, slug, ct);
        if (existing is null)
        {
            return Results.NotFound(new { error = $"No {type} with slug '{slug}'." });
        }

        existing.IsActive = false;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        existing.Version++;
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { type, slug, isActive = existing.IsActive });
    }

    private static IResult UnknownType(string type) =>
        Results.NotFound(new
        {
            error = $"Unknown content type: {type}",
            knownTypes = ContentEntityTypes.KnownTypes,
        });
}
