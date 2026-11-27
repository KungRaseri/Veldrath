using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Veldrath.Contracts.Foundry;
using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Features.Foundry;

/// <summary>
/// Minimal API endpoints for the community content portal.
/// POST   /api/foundry/submissions              — create submission [Authorize]
/// GET    /api/foundry/submissions              — list, narrowed to what the caller may see [Authorize]
/// GET    /api/foundry/submissions/{id}         — detail, same visibility rule [Authorize]
/// POST   /api/foundry/submissions/{id}/vote    — upvote/downvote [Authorize]
/// POST   /api/foundry/submissions/{id}/review  — approve/reject [content.review]
/// GET    /api/foundry/notifications            — get own notifications [Authorize]
/// GET    /api/foundry/notifications/unread-count — count own unread notifications [Authorize]
/// POST   /api/foundry/notifications/{id}/read  — mark notification read [Authorize]
/// </summary>
/// <remarks>
/// The submission feed is community content, but it is not anonymous and it is not uniform: an account that
/// cannot review sees its own submissions at every status plus other people's once they are approved, and a
/// reviewer sees everything. That rule lives here rather than in the portal so a second client cannot get it
/// wrong.
/// </remarks>
public static class FoundryEndpoints
{
    public static IEndpointRouteBuilder MapFoundryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/foundry").WithTags("Foundry");

        group.MapPost("/submissions", CreateAsync).RequireAuthorization().RequireRateLimiting("foundry-writes");
        group.MapGet("/submissions", ListAsync).RequireAuthorization();
        group.MapGet("/submissions/{id:guid}", GetAsync).RequireAuthorization();
        group.MapPost("/submissions/{id:guid}/vote", VoteAsync).RequireAuthorization().RequireRateLimiting("foundry-writes");
        // `content.review`, not the Curator role: an Admin holds every permission but no Curator role, and a
        // per-user grant has to work the same way the seeded role does.
        group.MapPost("/submissions/{id:guid}/review", ReviewAsync)
            .RequireAuthorization(Auth.Permissions.ReviewSubmissions)
            .RequireRateLimiting("foundry-writes");
        group.MapGet("/notifications", GetNotificationsAsync).RequireAuthorization();
        // Literal segment before the {id}-shaped route, so the badge can never be parsed as an id.
        group.MapGet("/notifications/unread-count", GetUnreadNotificationCountAsync).RequireAuthorization();
        group.MapPost("/notifications/{id:guid}/read", MarkNotificationReadAsync).RequireAuthorization();

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

    // GET /api/foundry/submissions?status=Pending&contentType=Item&search=sword&mine=true&page=1&pageSize=20
    private static async Task<IResult> ListAsync(
        string? status,
        string? contentType,
        string? search,
        bool? mine,
        int? page,
        int? pageSize,
        FoundryService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var callerId = GetAccountId(user);
        if (callerId is null) return Results.Unauthorized();

        var resolvedPage = Math.Max(1, page.GetValueOrDefault(1));
        var resolvedPageSize = Math.Clamp(pageSize.GetValueOrDefault(20), 1, 100);
        // `mine` narrows for everyone including a reviewer; without it the caller's authority decides.
        var scope = mine == true
            ? FoundrySubmissionScope.Own
            : IsReviewer(user)
                ? FoundrySubmissionScope.Everything
                : FoundrySubmissionScope.OwnPlusApproved;
        var results = await service.ListSubmissionsAsync(
            status,
            contentType,
            search,
            resolvedPage,
            resolvedPageSize,
            callerId.Value,
            scope,
            ct);
        return Results.Ok(results);
    }

    // GET /api/foundry/submissions/{id}
    private static async Task<IResult> GetAsync(
        Guid id,
        FoundryService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var callerId = GetAccountId(user);
        if (callerId is null) return Results.Unauthorized();

        var dto = await service.GetSubmissionAsync(id, ct);
        if (dto is null) return Results.NotFound();

        // Hidden rows 404 rather than 403: a rejected submission that exists is not something a stranger needs
        // to learn about, and a 403 would confirm the id.
        return FoundryVisibility.CanSee(callerId.Value, IsReviewer(user), dto)
            ? Results.Ok(dto)
            : Results.NotFound();
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

        var (dto, error) = await service.VoteAsync(id, voterId.Value, IsReviewer(user), request.Value, ct);
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

    // GET /api/foundry/notifications/unread-count
    private static async Task<IResult> GetUnreadNotificationCountAsync(
        FoundryService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var accountId = GetAccountId(user);
        if (accountId is null) return Results.Unauthorized();

        var count = await service.CountUnreadAsync(accountId.Value, ct);
        return Results.Ok(new FoundryUnreadNotificationCountDto(count));
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

    /// <summary>
    /// Whether the caller may read every submission. The decision endpoints require the permission claim, so
    /// the read path checks the same thing rather than the role.
    /// </summary>
    private static bool IsReviewer(ClaimsPrincipal user) =>
        user.HasClaim("permission", Auth.Permissions.ReviewSubmissions);
}
