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
/// Everything requires a signed-in account and nothing is anonymous: the graph carries hidden nodes and unlock
/// keys. Reading is open — the map is part of the community wiki — and writing is decided per map by
/// <see cref="MapCaller"/>: the account that created a map may write it, and <c>content.manage</c> may write
/// any. A save is validated by the same world validator the seed has to pass and is refused rather than
/// half-applied when the graph is wrong or the author is working from a stale copy.
/// </para>
/// <para>
/// Nothing here can make a map live. Approval is the gate, and it sits behind <c>content.review</c> — the
/// permission, not the <c>Curator</c> role, so an Admin or a hand-granted reviewer can decide as well as a
/// Curator: an author drafts and submits, a reviewer decides. Every lifecycle change is audited.
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
        // which made the read-only Foundry view unreachable for a Reviewer. Reads are open to every
        // signed-in account, which is what makes the portal public without exposing the graph anonymously.
        group.MapGet("", ListMapsAsync).RequireAuthorization();
        group.MapGet("/targets", GetTargetsAsync).RequireAuthorization();
        group.MapGet("/{mapId}", GetMapAsync).RequireAuthorization();

        // Authoring is decided per map by `MapCaller`, so these endpoints only require a session: the account
        // that created a map may write it, and `content.manage` may write any. A permission on the endpoint
        // made every map an editor's map, which is the opposite of community authorship.
        group.MapPost("/{mapId}/validate", ValidateMapAsync).RequireAuthorization();
        group.MapPut("/{mapId}", SaveMapAsync).RequireAuthorization();
        group.MapPost("", CreateMapAsync).RequireAuthorization();
        group.MapPost("/{mapId}/submit", SubmitMapAsync).RequireAuthorization();
        group.MapDelete("/{mapId}", DeleteMapAsync).RequireAuthorization();

        // The permission, not the role: `RequireAuthorization("Curator")` kept an Admin — who holds every
        // permission but not the role — out of the decision.
        group.MapPost("/{mapId}/review", ReviewMapAsync)
            .RequireAuthorization(Auth.Permissions.ReviewSubmissions);
        group.MapPost("/{mapId}/deactivate", DeactivateMapAsync)
            .RequireAuthorization(Auth.Permissions.ReviewSubmissions);

        return app;
    }

    private static async Task<IResult> ListMapsAsync(
        [FromServices] MapEditorService editor,
        ClaimsPrincipal user,
        CancellationToken cancellationToken) =>
        Results.Ok(await editor.ListAsync(Caller(user), cancellationToken));

    private static async Task<IResult> GetTargetsAsync(
        [FromServices] MapEditorService editor,
        ClaimsPrincipal user,
        CancellationToken cancellationToken) =>
        Results.Ok(await editor.GetTargetsAsync(Caller(user), cancellationToken));

    private static async Task<IResult> GetMapAsync(
        string mapId,
        [FromServices] MapEditorService editor,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var map = await editor.GetAsync(mapId, Caller(user), cancellationToken);
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
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var result = await editor.SaveAsync(
            mapId,
            request.Map,
            request.ExpectedVersion,
            Caller(user),
            cancellationToken);

        return result.Status switch
        {
            "ok" => Results.Ok(result),
            "forbidden" => Results.Json(result, statusCode: StatusCodes.Status403Forbidden),
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
        var caller = Caller(user);
        if (caller.AccountId == Guid.Empty) return Results.Unauthorized();

        var result = await editor.SubmitAsync(mapId, caller, cancellationToken);
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
        [FromServices] FoundryNotificationWriter notifications,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var result = await editor.DeactivateAsync(mapId, cancellationToken);

        // Retiring a live map is as consequential as rejecting it, so its author hears about it the same way.
        // An extra read after a successful write is the cheapest way to say that: the author and the name are
        // the two facts the lifecycle response does not carry.
        if (result.Success
            && await editor.AuthorOfAsync(mapId, cancellationToken) is { } map
            && map.AccountId is { } author)
        {
            await notifications.NotifyAsync(
                author,
                FoundryNotificationKind.MapReview,
                map.Name,
                $"Your map \"{map.Name}\" was retired by a reviewer and is no longer in the game.",
                mapId: mapId,
                cancellationToken: cancellationToken);
        }

        return await LifecycleAsync(db, user, result, "deactivate_map", mapId, null);
    }

    private static async Task<IResult> DeleteMapAsync(
        string mapId,
        [FromServices] MapEditorService editor,
        [FromServices] ApplicationDbContext db,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var result = await editor.DeleteDraftAsync(mapId, Caller(user), cancellationToken);
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
            "forbidden" => Results.Json(result, statusCode: StatusCodes.Status403Forbidden),
            "not_found" => Results.NotFound(result),
            "conflict" => Results.Conflict(result),
            _ => Results.BadRequest(result),
        };
    }

    /// <summary>
    /// Reads the caller's authority off the enriched token. The Admin role is treated as holding everything,
    /// mirroring the web's `hasPermission`, so a token whose permission claims were not enriched still works.
    /// </summary>
    private static MapCaller Caller(ClaimsPrincipal user)
    {
        var isAdmin = user.IsInRole(Auth.Roles.Admin);

        return new MapCaller(
            GetAccountId(user) ?? Guid.Empty,
            isAdmin || user.HasClaim("permission", Auth.Permissions.ManageContent),
            isAdmin || user.HasClaim("permission", Auth.Permissions.ReviewSubmissions));
    }

    private static Guid? GetAccountId(ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.TryParse(subject, out var id) ? id : null;
    }
}
