using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using RealmUnbound.Contracts.Foundry;

namespace RealmUnbound.Server.Features.Foundry;

/// <summary>
/// Minimal API endpoints for the community content portal.
/// POST   /api/foundry/submissions              — create submission [Authorize]
/// GET    /api/foundry/submissions              — list (filter by type/status)
/// GET    /api/foundry/submissions/{id}         — detail
/// POST   /api/foundry/submissions/{id}/vote    — upvote/downvote [Authorize]
/// POST   /api/foundry/submissions/{id}/review  — approve/reject [Authorize(Curator+)]
/// </summary>
public static class FoundryEndpoints
{
    public static IEndpointRouteBuilder MapFoundryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/foundry").WithTags("Foundry");

        group.MapPost("/submissions",                  CreateAsync).RequireAuthorization();
        group.MapGet("/submissions",                   ListAsync);
        group.MapGet("/submissions/{id:guid}",         GetAsync);
        group.MapPost("/submissions/{id:guid}/vote",   VoteAsync).RequireAuthorization();
        group.MapPost("/submissions/{id:guid}/review", ReviewAsync).RequireAuthorization();

        return app;
    }

    // POST /api/foundry/submissions
    private static async Task<IResult> CreateAsync(
        [FromBody] CreateSubmissionRequest request,
        FoundryService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var submitterId = GetAccountId(user);
        if (submitterId is null) return Results.Unauthorized();

        var (dto, error) = await service.CreateSubmissionAsync(request, submitterId.Value, ct);
        return dto is not null
            ? Results.Created($"/api/foundry/submissions/{dto.Id}", dto)
            : Results.BadRequest(new { error });
    }

    // GET /api/foundry/submissions?status=Pending&contentType=Item
    private static async Task<IResult> ListAsync(
        string? status,
        string? contentType,
        FoundryService service,
        CancellationToken ct)
    {
        var results = await service.ListSubmissionsAsync(status, contentType, ct);
        return Results.Ok(results);
    }

    // GET /api/foundry/submissions/{id}
    private static async Task<IResult> GetAsync(
        Guid id,
        FoundryService service,
        CancellationToken ct)
    {
        var dto = await service.GetSubmissionAsync(id, ct);
        return dto is not null ? Results.Ok(dto) : Results.NotFound();
    }

    // POST /api/foundry/submissions/{id}/vote
    private static async Task<IResult> VoteAsync(
        Guid id,
        [FromBody] VoteRequest request,
        FoundryService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var voterId = GetAccountId(user);
        if (voterId is null) return Results.Unauthorized();

        var (dto, error) = await service.VoteAsync(id, voterId.Value, request.Value, ct);
        return dto is not null
            ? Results.Ok(dto)
            : Results.BadRequest(new { error });
    }

    // POST /api/foundry/submissions/{id}/review
    // Restricted to Curator and Archivist roles (enforced at the application layer
    // via role checks in the service once FoundryRole system lands; for now any
    // authenticated user can call the endpoint — the TODO is noted in FoundryService).
    private static async Task<IResult> ReviewAsync(
        Guid id,
        [FromBody] ReviewRequest request,
        FoundryService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var reviewerId = GetAccountId(user);
        if (reviewerId is null) return Results.Unauthorized();

        var (dto, error) = await service.ReviewAsync(id, reviewerId.Value, request, ct);
        return dto is not null
            ? Results.Ok(dto)
            : Results.BadRequest(new { error });
    }

    private static Guid? GetAccountId(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? user.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
