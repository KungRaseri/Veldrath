using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Veldrath.Contracts.Map;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Features.Foundry;

namespace Veldrath.Server.Features.Content;

/// <summary>
/// The Foundry map editor's read, write and lifecycle path.
/// </summary>
/// <remarks>
/// <para>
/// Reads require <c>content.view</c> — the graph carries hidden nodes and unlock keys, so it is never
/// anonymous — and authoring requires <c>content.manage</c>. A save is validated by the same world validator
/// the seed has to pass and is refused rather than half-applied when the graph is wrong or the author is
/// working from a stale copy.
/// </para>
/// <para>
/// Nothing here can make a map live. Approval is the gate, and it sits behind the <c>Curator</c> policy: an
/// author drafts and submits, a reviewer decides. Every lifecycle change is audited.
/// </para>
/// </remarks>
public static class MapEditorEndpoints
{
    /// <summary>Registers the map authoring endpoints.</summary>
    /// <param name="app">Route builder to register on.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapMapEditorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/content/maps").WithTags("Content");

        // Per endpoint rather than group-wide: the group used to require ManageContent for everything,
        // which made the read-only Foundry view unreachable for a Reviewer.
        group.MapGet("", ListMapsAsync).RequireAuthorization(Auth.Permissions.ViewContent);
        group.MapGet("/targets", GetTargetsAsync).RequireAuthorization(Auth.Permissions.ViewContent);
        group.MapGet("/{mapId}", GetMapAsync).RequireAuthorization(Auth.Permissions.ViewContent);
        group.MapPost("/{mapId}/validate", ValidateMapAsync)
            .RequireAuthorization(Auth.Permissions.ManageContent);
        group.MapPut("/{mapId}", SaveMapAsync).RequireAuthorization(Auth.Permissions.ManageContent);

        group.MapPost("", CreateMapAsync).RequireAuthorization(Auth.Permissions.ManageContent);
        group.MapPost("/{mapId}/submit", SubmitMapAsync)
            .RequireAuthorization(Auth.Permissions.ManageContent);
        group.MapPost("/{mapId}/review", ReviewMapAsync).RequireAuthorization("Curator");
        group.MapPost("/{mapId}/deactivate", DeactivateMapAsync).RequireAuthorization("Curator");
        group.MapDelete("/{mapId}", DeleteMapAsync).RequireAuthorization(Auth.Permissions.ManageContent);

        return app;
    }

    private static async Task<IResult> ListMapsAsync(
        [FromServices] MapEditorService editor,
        CancellationToken cancellationToken) =>
        Results.Ok(await editor.ListAsync(cancellationToken));

    private static async Task<IResult> GetTargetsAsync(
        [FromServices] MapEditorService editor,
        CancellationToken cancellationToken) =>
        Results.Ok(await editor.GetTargetsAsync(cancellationToken));

    private static async Task<IResult> GetMapAsync(
        string mapId,
        [FromServices] MapEditorService editor,
        CancellationToken cancellationToken)
    {
        var map = await editor.GetAsync(mapId, cancellationToken);
        return map is null ? Results.NotFound() : Results.Ok(map);
    }

    private static async Task<IResult> ValidateMapAsync(
        string mapId,
        [FromBody] MapGraphValidationRequest request,
        [FromServices] MapEditorService editor,
        CancellationToken cancellationToken)
    {
        var result = await editor.ValidateAsync(mapId, request.Map, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> SaveMapAsync(
        string mapId,
        [FromBody] MapGraphWriteRequest request,
        [FromServices] MapEditorService editor,
        CancellationToken cancellationToken)
    {
        var result = await editor.SaveAsync(mapId, request.Map, request.ExpectedVersion, cancellationToken);

        return result.Status switch
        {
            "ok" => Results.Ok(result),
            "not_found" => Results.NotFound(result),
            "conflict" => Results.Conflict(result),
            _ => Results.BadRequest(result),
        };
    }

    private static async Task<IResult> CreateMapAsync(
        [FromBody] MapCreateRequest request,
        [FromServices] MapEditorService editor,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var accountId = GetAccountId(user);
        if (accountId is null)
            return Results.Unauthorized();

        var (map, error) = await editor.CreateDraftAsync(request, accountId.Value, cancellationToken);
        if (map is null)
            return Results.BadRequest(new { error });

        return Results.Created($"/api/content/maps/{Uri.EscapeDataString(map.Id)}", map);
    }

    private static async Task<IResult> SubmitMapAsync(
        string mapId,
        [FromServices] MapEditorService editor,
        [FromServices] ApplicationDbContext db,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var accountId = GetAccountId(user);
        if (accountId is null)
            return Results.Unauthorized();

        var result = await editor.SubmitAsync(mapId, accountId.Value, cancellationToken);
        return await LifecycleAsync(db, user, result, "submit_map", mapId, null);
    }

    private static async Task<IResult> ReviewMapAsync(
        string mapId,
        [FromBody] MapReviewRequest request,
        [FromServices] MapEditorService editor,
        [FromServices] ApplicationDbContext db,
        [FromServices] FoundryNotificationWriter notifications,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var reviewerId = GetAccountId(user);
        if (reviewerId is null)
            return Results.Unauthorized();

        var outcome = await editor.ReviewAsync(mapId, reviewerId.Value, request, cancellationToken);

        // The author is told in the inbox they already use for submission decisions. Nothing to notify for
        // seeded content: it has neither a creator nor a submitter.
        if (outcome.Response.Success && outcome.AuthorAccountId is { } author)
        {
            await notifications.NotifyAsync(
                author,
                FoundryNotificationKind.MapReview,
                outcome.MapName,
                request.Approved
                    ? $"Your map \"{outcome.MapName}\" was approved and is now live."
                    : $"Your map \"{outcome.MapName}\" was rejected. {request.Notes}",
                mapId: mapId,
                cancellationToken: cancellationToken);
        }

        return await LifecycleAsync(
            db,
            user,
            outcome.Response,
            request.Approved ? "approve_map" : "reject_map",
            mapId,
            request.Notes);
    }

    private static async Task<IResult> DeactivateMapAsync(
        string mapId,
        [FromServices] MapEditorService editor,
        [FromServices] ApplicationDbContext db,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var result = await editor.DeactivateAsync(mapId, cancellationToken);
        return await LifecycleAsync(db, user, result, "deactivate_map", mapId, null);
    }

    private static async Task<IResult> DeleteMapAsync(
        string mapId,
        [FromServices] MapEditorService editor,
        [FromServices] ApplicationDbContext db,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var result = await editor.DeleteDraftAsync(mapId, cancellationToken);
        return await LifecycleAsync(db, user, result, "delete_map", mapId, null);
    }

    /// <summary>Maps a lifecycle outcome onto a response and audits the ones that changed something.</summary>
    private static async Task<IResult> LifecycleAsync(
        ApplicationDbContext db,
        ClaimsPrincipal user,
        MapLifecycleResponse result,
        string action,
        string mapId,
        string? notes)
    {
        if (result.Success)
        {
            db.AdminAuditEntries.Add(new AdminAuditEntry
            {
                ActorAccountId = GetAccountId(user) ?? Guid.Empty,
                ActorUsername = user.Identity?.Name ?? user.FindFirstValue(ClaimTypes.Name) ?? "?",
                Action = action,
                Details = notes is null ? mapId : $"{mapId}: {notes}",
                OccurredAt = DateTimeOffset.UtcNow,
            });

            await db.SaveChangesAsync();
        }

        return result.Status switch
        {
            "ok" => Results.Ok(result),
            "not_found" => Results.NotFound(result),
            "conflict" => Results.Conflict(result),
            _ => Results.BadRequest(result),
        };
    }

    private static Guid? GetAccountId(ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.TryParse(subject, out var id) ? id : null;
    }
}
