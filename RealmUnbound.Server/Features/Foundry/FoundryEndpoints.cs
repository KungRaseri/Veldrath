using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using RealmUnbound.Contracts.Foundry;

namespace RealmUnbound.Server.Features.Foundry;

/// <summary>
/// Minimal API endpoints for the community content portal.
/// POST   /api/foundry/submissions              — create submission [Authorize]
/// GET    /api/foundry/submissions              — list (filter by type/status/search, paginated)
/// GET    /api/foundry/submissions/{id}         — detail
/// POST   /api/foundry/submissions/{id}/vote    — upvote/downvote [Authorize]
/// POST   /api/foundry/submissions/{id}/review  — approve/reject [Authorize(Curator)]
/// GET    /api/foundry/notifications            — get own notifications [Authorize]
/// POST   /api/foundry/notifications/{id}/read  — mark notification read [Authorize]
/// </summary>
public static class FoundryEndpoints
{
    public static IEndpointRouteBuilder MapFoundryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/foundry").WithTags("Foundry");

        group.MapPost("/submissions",                     CreateAsync).RequireAuthorization().RequireRateLimiting("foundry-writes");
        group.MapGet("/submissions",                      ListAsync);
        group.MapGet("/submissions/{id:guid}",            GetAsync);
        group.MapPost("/submissions/{id:guid}/vote",      VoteAsync).RequireAuthorization().RequireRateLimiting("foundry-writes");
        group.MapPost("/submissions/{id:guid}/review",    ReviewAsync).RequireAuthorization("Curator");
        group.MapGet("/notifications",                    GetNotificationsAsync).RequireAuthorization();
        group.MapPost("/notifications/{id:guid}/read",    MarkNotificationReadAsync).RequireAuthorization();

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

    // GET /api/foundry/submissions?status=Pending&contentType=Item&search=sword&page=1&pageSize=20
    private static async Task<IResult> ListAsync(
        string? status,
        string? contentType,
        string? search,
        int? page,
        int? pageSize,
        FoundryService service,
        CancellationToken ct)
    {
        var resolvedPage     = Math.Max(1, page.GetValueOrDefault(1));
        var resolvedPageSize = Math.Clamp(pageSize.GetValueOrDefault(20), 1, 100);
        var results = await service.ListSubmissionsAsync(status, contentType, search, resolvedPage, resolvedPageSize, ct);
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

    // POST /api/foundry/submissions/{id}/review  [Curator]
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

    // GET /api/foundry/notifications
    private static async Task<IResult> GetNotificationsAsync(
        FoundryService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var accountId = GetAccountId(user);
        if (accountId is null) return Results.Unauthorized();

        var notifications = await service.GetNotificationsAsync(accountId.Value, ct);
        return Results.Ok(notifications);
    }

    // POST /api/foundry/notifications/{id}/read
    private static async Task<IResult> MarkNotificationReadAsync(
        Guid id,
        FoundryService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var accountId = GetAccountId(user);
        if (accountId is null) return Results.Unauthorized();

        var found = await service.MarkNotificationReadAsync(id, accountId.Value, ct);
        return found ? Results.NoContent() : Results.NotFound();
    }

    private static Guid? GetAccountId(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? user.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
