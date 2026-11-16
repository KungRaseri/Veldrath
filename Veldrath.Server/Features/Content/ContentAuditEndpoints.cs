using Veldrath.Contracts.Content;
using Veldrath.Server.Features.Auth;

namespace Veldrath.Server.Features.Content;

/// <summary>
/// The content audit, on demand.
/// </summary>
/// <remarks>
/// <para>
/// The same rules the host runs at startup and CI runs as a guard, exposed for the portal's world-health page.
/// Before this there was no way to see them without reading a log.
/// </para>
/// <para>
/// It walks the whole catalog, so it is a spot check rather than something to poll. Reads require
/// <c>content.view</c>: the issues name entities that may be hidden from players.
/// </para>
/// </remarks>
public static class ContentAuditEndpoints
{
    /// <summary>Maps the content audit endpoint.</summary>
    /// <param name="app">Endpoint route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapContentAuditEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/content/audit", GetAuditAsync)
            .WithTags("Content")
            .WithSummary("Runs the content audit and returns every issue it finds.")
            .RequireAuthorization(Permissions.ViewContent);

        return app;
    }

    private static async Task<IResult> GetAuditAsync(
        ContentAuditService audit,
        CancellationToken ct)
    {
        var issues = await audit.RunAsync(ct);

        return Results.Ok(
            issues.Select(issue => new ContentAuditIssueDto(
                issue.Rule,
                issue.Message,
                issue.Severity.ToString(),
                issue.Field,
                issue.Identifier))
        );
    }
}
