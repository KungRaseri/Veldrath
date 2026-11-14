using Microsoft.AspNetCore.Mvc;
using Veldrath.Contracts.Map;

namespace Veldrath.Server.Features.Content;

/// <summary>
/// The Foundry map editor's read and write path. Reads require <c>content.view</c> — the graph carries
/// hidden nodes and unlock keys, so it is never anonymous — and writes require <c>content.manage</c>. A
/// save is validated by the same world validator the seed has to pass and is refused rather than
/// half-applied when the graph is wrong or the author is working from a stale copy.
/// </summary>
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
}
