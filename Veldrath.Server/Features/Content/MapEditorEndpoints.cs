using Microsoft.AspNetCore.Mvc;
using Veldrath.Contracts.Map;

namespace Veldrath.Server.Features.Content;

/// <summary>
/// The Foundry map editor's write path. Reads are open to anyone with the content permission; the save is
/// validated by the same world validator the seed has to pass and is refused rather than half-applied when
/// the graph is wrong or the author is working from a stale copy.
/// </summary>
public static class MapEditorEndpoints
{
    /// <summary>Registers the map authoring endpoints.</summary>
    /// <param name="app">Route builder to register on.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapMapEditorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/content/maps")
            .WithTags("Content")
            .RequireAuthorization(Auth.Permissions.ManageContent);

        group.MapGet("", ListMapsAsync);
        group.MapGet("/{mapId}", GetMapAsync);
        group.MapPut("/{mapId}", SaveMapAsync);

        return app;
    }

    private static async Task<IResult> ListMapsAsync(
        [FromServices] MapEditorService editor,
        CancellationToken cancellationToken) =>
        Results.Ok(await editor.ListAsync(cancellationToken));

    private static async Task<IResult> GetMapAsync(
        string mapId,
        [FromServices] MapEditorService editor,
        CancellationToken cancellationToken)
    {
        var map = await editor.GetAsync(mapId, cancellationToken);
        return map is null ? Results.NotFound() : Results.Ok(map);
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
