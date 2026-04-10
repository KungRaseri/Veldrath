using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RealmUnbound.Contracts.Admin;
using RealmUnbound.Server.Data;
using RealmUnbound.Server.Data.Entities;

namespace RealmUnbound.Server.Features.Reports;

/// <summary>
/// Minimal API endpoints for player-submitted reports.
/// POST /api/reports — submit a report against another player [Authorize]
/// </summary>
public static class ReportEndpoints
{
    /// <summary>Registers all report endpoints on the provided route builder.</summary>
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports")
            .WithTags("Reports")
            .RequireAuthorization()
            .RequireRateLimiting("foundry-writes");

        group.MapPost("", SubmitReportAsync);

        return app;
    }

    // POST /api/reports
    private static async Task<IResult> SubmitReportAsync(
        [FromBody] SubmitReportRequest request,
        ClaimsPrincipal actor,
        [FromServices] UserManager<PlayerAccount> userManager,
        [FromServices] ApplicationDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.TargetUsername))
            return Results.BadRequest(new { error = "Target username is required." });

        if (string.IsNullOrWhiteSpace(request.Reason))
            return Results.BadRequest(new { error = "Reason is required." });

        var target = await userManager.FindByNameAsync(request.TargetUsername);
        if (target is null)
            return Results.NotFound(new { error = $"User '{request.TargetUsername}' not found." });

        // Prevent players from reporting themselves.
        var reporterIdStr = actor.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(reporterIdStr, out var reporterId) && reporterId == target.Id)
            return Results.BadRequest(new { error = "You cannot report yourself." });

        var reporterUsername = actor.FindFirstValue(ClaimTypes.Name) ?? "unknown";

        db.PlayerReports.Add(new PlayerReport
        {
            ReporterName = reporterUsername,
            TargetName   = target.UserName ?? request.TargetUsername,
            Reason       = request.Reason,
            SubmittedAt  = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(ct);

        return Results.Created();
    }
}
