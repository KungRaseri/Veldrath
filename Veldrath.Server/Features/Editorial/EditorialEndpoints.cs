using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Veldrath.Contracts.Editorial;
using Veldrath.Contracts.Foundry;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities.Editorial;
using Veldrath.Server.Features.Auth;

namespace Veldrath.Server.Features.Editorial;

/// <summary>
/// Minimal API endpoints for editorial content (patch notes, lore articles, announcements).
///
/// Public (anonymous):
///   GET  /api/editorial/patch-notes               — published list (paged)
///   GET  /api/editorial/patch-notes/{slug}         — published entry by slug
///   GET  /api/editorial/lore                       — published list (paged, ?category=)
///   GET  /api/editorial/lore/{slug}                — published article by slug
///   GET  /api/editorial/announcements              — published announcements (paged)
///
/// Admin (manage_content permission + foundry-writes rate limit):
///   GET    /api/editorial/admin/patch-notes              — all (including drafts)
///   POST   /api/editorial/admin/patch-notes              — create
///   GET    /api/editorial/admin/patch-notes/{id}         — get by id
///   PUT    /api/editorial/admin/patch-notes/{id}         — update
///   DELETE /api/editorial/admin/patch-notes/{id}         — delete
///   POST   /api/editorial/admin/patch-notes/{id}/publish — toggle publish
///   (same CRUD + publish for lore and announcements)
/// </summary>
public static class EditorialEndpoints
{
    /// <summary>Registers all editorial endpoints on the provided route builder.</summary>
    public static IEndpointRouteBuilder MapEditorialEndpoints(this IEndpointRouteBuilder app)
    {
        // ── Public read endpoints ──────────────────────────────────────────────────
        var pub = app.MapGroup("/api/editorial").WithTags("Editorial");

        pub.MapGet("/patch-notes",        ListPatchNotesAsync);
        pub.MapGet("/patch-notes/{slug}", GetPatchNoteBySlugAsync);
        pub.MapGet("/lore",               ListLoreArticlesAsync);
        pub.MapGet("/lore/{slug}",        GetLoreArticleBySlugAsync);
        pub.MapGet("/announcements",      ListAnnouncementsAsync);

        // ── Admin endpoints ────────────────────────────────────────────────────────
        var admin = app.MapGroup("/api/editorial/admin")
            .WithTags("Editorial")
            .RequireAuthorization(Permissions.ManageContent)
            .RequireRateLimiting("foundry-writes");

        // Patch notes
        admin.MapGet("/patch-notes",                  AdminListPatchNotesAsync);
        admin.MapPost("/patch-notes",                 AdminCreatePatchNoteAsync);
        admin.MapGet("/patch-notes/{id:guid}",        AdminGetPatchNoteAsync);
        admin.MapPut("/patch-notes/{id:guid}",        AdminUpdatePatchNoteAsync);
        admin.MapDelete("/patch-notes/{id:guid}",     AdminDeletePatchNoteAsync);
        admin.MapPost("/patch-notes/{id:guid}/publish", AdminTogglePatchNotePublishAsync);

        // Lore articles
        admin.MapGet("/lore",                     AdminListLoreArticlesAsync);
        admin.MapPost("/lore",                    AdminCreateLoreArticleAsync);
        admin.MapGet("/lore/{id:guid}",           AdminGetLoreArticleAsync);
        admin.MapPut("/lore/{id:guid}",           AdminUpdateLoreArticleAsync);
        admin.MapDelete("/lore/{id:guid}",        AdminDeleteLoreArticleAsync);
        admin.MapPost("/lore/{id:guid}/publish",  AdminToggleLoreArticlePublishAsync);

        // Announcements
        admin.MapGet("/announcements",                      AdminListAnnouncementsAsync);
        admin.MapPost("/announcements",                     AdminCreateAnnouncementAsync);
        admin.MapGet("/announcements/{id:guid}",            AdminGetAnnouncementAsync);
        admin.MapPut("/announcements/{id:guid}",            AdminUpdateAnnouncementAsync);
        admin.MapDelete("/announcements/{id:guid}",         AdminDeleteAnnouncementAsync);
        admin.MapPost("/announcements/{id:guid}/publish",   AdminToggleAnnouncementPublishAsync);

        return app;
    }

    // ── Public endpoints ───────────────────────────────────────────────────────────

    // GET /api/editorial/patch-notes?page=1&pageSize=20
    private static async Task<IResult> ListPatchNotesAsync(
        int? page,
        int? pageSize,
        EditorialDbContext db,
        CancellationToken ct)
    {
        var p    = Math.Max(1, page.GetValueOrDefault(1));
        var size = Math.Clamp(pageSize.GetValueOrDefault(20), 1, 100);

        var total = await db.PatchNotes
            .Where(x => x.Status == EditorialStatus.Published)
            .CountAsync(ct);

        var items = await db.PatchNotes
            .Where(x => x.Status == EditorialStatus.Published)
            .OrderByDescending(x => x.PublishedAt)
            .Skip((p - 1) * size)
            .Take(size)
            .Select(x => new PatchNoteSummaryDto(
                x.Id, x.Slug, x.Title, x.Summary, x.Version,
                x.Status.ToString(), x.PublishedAt))
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<PatchNoteSummaryDto>(items, total, p, size));
    }

    // GET /api/editorial/patch-notes/{slug}
    private static async Task<IResult> GetPatchNoteBySlugAsync(
        string slug,
        EditorialDbContext db,
        CancellationToken ct)
    {
        var note = await db.PatchNotes
            .Where(x => x.Slug == slug && x.Status == EditorialStatus.Published)
            .Select(x => new PatchNoteDto(
                x.Id, x.Slug, x.Title, x.Summary, x.Version,
                x.Status.ToString(), x.PublishedAt, x.Content))
            .FirstOrDefaultAsync(ct);

        return note is not null ? Results.Ok(note) : Results.NotFound();
    }

    // GET /api/editorial/lore?category=History&page=1&pageSize=20
    private static async Task<IResult> ListLoreArticlesAsync(
        string? category,
        int? page,
        int? pageSize,
        EditorialDbContext db,
        CancellationToken ct)
    {
        var p    = Math.Max(1, page.GetValueOrDefault(1));
        var size = Math.Clamp(pageSize.GetValueOrDefault(20), 1, 100);

        var query = db.LoreArticles.Where(x => x.Status == EditorialStatus.Published);
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(x => x.Category == category);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.PublishedAt)
            .Skip((p - 1) * size)
            .Take(size)
            .Select(x => new LoreArticleSummaryDto(
                x.Id, x.Slug, x.Title, x.Summary, x.Category,
                x.Status.ToString(), x.PublishedAt))
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<LoreArticleSummaryDto>(items, total, p, size));
    }

    // GET /api/editorial/lore/{slug}
    private static async Task<IResult> GetLoreArticleBySlugAsync(
        string slug,
        EditorialDbContext db,
        CancellationToken ct)
    {
        var article = await db.LoreArticles
            .Where(x => x.Slug == slug && x.Status == EditorialStatus.Published)
            .Select(x => new LoreArticleDto(
                x.Id, x.Slug, x.Title, x.Summary, x.Category,
                x.Status.ToString(), x.PublishedAt, x.Content))
            .FirstOrDefaultAsync(ct);

        return article is not null ? Results.Ok(article) : Results.NotFound();
    }

    // GET /api/editorial/announcements?page=1&pageSize=20
    private static async Task<IResult> ListAnnouncementsAsync(
        int? page,
        int? pageSize,
        EditorialDbContext db,
        CancellationToken ct)
    {
        var p    = Math.Max(1, page.GetValueOrDefault(1));
        var size = Math.Clamp(pageSize.GetValueOrDefault(20), 1, 100);

        var total = await db.Announcements
            .Where(x => x.Status == EditorialStatus.Published)
            .CountAsync(ct);

        var items = await db.Announcements
            .Where(x => x.Status == EditorialStatus.Published)
            .OrderByDescending(x => x.PublishedAt)
            .Skip((p - 1) * size)
            .Take(size)
            .Select(x => new EditorialAnnouncementDto(
                x.Id, x.Title, x.Body, x.Status.ToString(), x.PublishedAt))
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<EditorialAnnouncementDto>(items, total, p, size));
    }

    // ── Admin: Patch Notes ─────────────────────────────────────────────────────────

    // GET /api/editorial/admin/patch-notes?page=1&pageSize=20
    private static async Task<IResult> AdminListPatchNotesAsync(
        int? page,
        int? pageSize,
        EditorialDbContext db,
        CancellationToken ct)
    {
        var p    = Math.Max(1, page.GetValueOrDefault(1));
        var size = Math.Clamp(pageSize.GetValueOrDefault(20), 1, 100);

        var total = await db.PatchNotes.CountAsync(ct);

        var items = await db.PatchNotes
            .OrderByDescending(x => x.CreatedAt)
            .Skip((p - 1) * size)
            .Take(size)
            .Select(x => new PatchNoteSummaryDto(
                x.Id, x.Slug, x.Title, x.Summary, x.Version,
                x.Status.ToString(), x.PublishedAt))
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<PatchNoteSummaryDto>(items, total, p, size));
    }

    // POST /api/editorial/admin/patch-notes
    private static async Task<IResult> AdminCreatePatchNoteAsync(
        [FromBody] CreatePatchNoteRequest request,
        ClaimsPrincipal user,
        EditorialDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Slug)) return Results.BadRequest(new { error = "Slug is required." });
        if (string.IsNullOrWhiteSpace(request.Title)) return Results.BadRequest(new { error = "Title is required." });

        var slugTaken = await db.PatchNotes.AnyAsync(x => x.Slug == request.Slug, ct);
        if (slugTaken) return Results.Conflict(new { error = "A patch note with that slug already exists." });

        var note = new PatchNote
        {
            Slug            = request.Slug,
            Title           = request.Title,
            Content         = request.Content,
            Summary         = request.Summary,
            Version         = request.Version,
            AuthorAccountId = GetAccountId(user),
        };

        db.PatchNotes.Add(note);
        await db.SaveChangesAsync(ct);

        var dto = ToPatchNoteDto(note);
        return Results.Created($"/api/editorial/admin/patch-notes/{note.Id}", dto);
    }

    // GET /api/editorial/admin/patch-notes/{id}
    private static async Task<IResult> AdminGetPatchNoteAsync(
        Guid id,
        EditorialDbContext db,
        CancellationToken ct)
    {
        var note = await db.PatchNotes.FindAsync([id], ct);
        return note is not null ? Results.Ok(ToPatchNoteDto(note)) : Results.NotFound();
    }

    // PUT /api/editorial/admin/patch-notes/{id}
    private static async Task<IResult> AdminUpdatePatchNoteAsync(
        Guid id,
        [FromBody] UpdatePatchNoteRequest request,
        EditorialDbContext db,
        CancellationToken ct)
    {
        var note = await db.PatchNotes.FindAsync([id], ct);
        if (note is null) return Results.NotFound();

        if (!Enum.TryParse<EditorialStatus>(request.Status, ignoreCase: true, out var status))
            return Results.BadRequest(new { error = "Invalid status. Use 'Draft' or 'Published'." });

        note.Title     = request.Title;
        note.Content   = request.Content;
        note.Summary   = request.Summary;
        note.Version   = request.Version;
        note.Status    = status;
        note.UpdatedAt = DateTimeOffset.UtcNow;

        if (status == EditorialStatus.Published && note.PublishedAt is null)
            note.PublishedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return Results.Ok(ToPatchNoteDto(note));
    }

    // DELETE /api/editorial/admin/patch-notes/{id}
    private static async Task<IResult> AdminDeletePatchNoteAsync(
        Guid id,
        EditorialDbContext db,
        CancellationToken ct)
    {
        var note = await db.PatchNotes.FindAsync([id], ct);
        if (note is null) return Results.NotFound();

        db.PatchNotes.Remove(note);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    // POST /api/editorial/admin/patch-notes/{id}/publish
    private static async Task<IResult> AdminTogglePatchNotePublishAsync(
        Guid id,
        EditorialDbContext db,
        CancellationToken ct)
    {
        var note = await db.PatchNotes.FindAsync([id], ct);
        if (note is null) return Results.NotFound();

        if (note.Status == EditorialStatus.Draft)
        {
            note.Status      = EditorialStatus.Published;
            note.PublishedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            note.Status = EditorialStatus.Draft;
        }

        note.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.Ok(ToPatchNoteDto(note));
    }

    // ── Admin: Lore Articles ───────────────────────────────────────────────────────

    // GET /api/editorial/admin/lore?page=1&pageSize=20
    private static async Task<IResult> AdminListLoreArticlesAsync(
        int? page,
        int? pageSize,
        EditorialDbContext db,
        CancellationToken ct)
    {
        var p    = Math.Max(1, page.GetValueOrDefault(1));
        var size = Math.Clamp(pageSize.GetValueOrDefault(20), 1, 100);

        var total = await db.LoreArticles.CountAsync(ct);

        var items = await db.LoreArticles
            .OrderByDescending(x => x.CreatedAt)
            .Skip((p - 1) * size)
            .Take(size)
            .Select(x => new LoreArticleSummaryDto(
                x.Id, x.Slug, x.Title, x.Summary, x.Category,
                x.Status.ToString(), x.PublishedAt))
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<LoreArticleSummaryDto>(items, total, p, size));
    }

    // POST /api/editorial/admin/lore
    private static async Task<IResult> AdminCreateLoreArticleAsync(
        [FromBody] CreateLoreArticleRequest request,
        ClaimsPrincipal user,
        EditorialDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Slug)) return Results.BadRequest(new { error = "Slug is required." });
        if (string.IsNullOrWhiteSpace(request.Title)) return Results.BadRequest(new { error = "Title is required." });

        var slugTaken = await db.LoreArticles.AnyAsync(x => x.Slug == request.Slug, ct);
        if (slugTaken) return Results.Conflict(new { error = "A lore article with that slug already exists." });

        var article = new LoreArticle
        {
            Slug            = request.Slug,
            Title           = request.Title,
            Content         = request.Content,
            Summary         = request.Summary,
            Category        = request.Category,
            AuthorAccountId = GetAccountId(user),
        };

        db.LoreArticles.Add(article);
        await db.SaveChangesAsync(ct);

        var dto = ToLoreArticleDto(article);
        return Results.Created($"/api/editorial/admin/lore/{article.Id}", dto);
    }

    // GET /api/editorial/admin/lore/{id}
    private static async Task<IResult> AdminGetLoreArticleAsync(
        Guid id,
        EditorialDbContext db,
        CancellationToken ct)
    {
        var article = await db.LoreArticles.FindAsync([id], ct);
        return article is not null ? Results.Ok(ToLoreArticleDto(article)) : Results.NotFound();
    }

    // PUT /api/editorial/admin/lore/{id}
    private static async Task<IResult> AdminUpdateLoreArticleAsync(
        Guid id,
        [FromBody] UpdateLoreArticleRequest request,
        EditorialDbContext db,
        CancellationToken ct)
    {
        var article = await db.LoreArticles.FindAsync([id], ct);
        if (article is null) return Results.NotFound();

        if (!Enum.TryParse<EditorialStatus>(request.Status, ignoreCase: true, out var status))
            return Results.BadRequest(new { error = "Invalid status. Use 'Draft' or 'Published'." });

        article.Title    = request.Title;
        article.Content  = request.Content;
        article.Summary  = request.Summary;
        article.Category = request.Category;
        article.Status   = status;
        article.UpdatedAt = DateTimeOffset.UtcNow;

        if (status == EditorialStatus.Published && article.PublishedAt is null)
            article.PublishedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return Results.Ok(ToLoreArticleDto(article));
    }

    // DELETE /api/editorial/admin/lore/{id}
    private static async Task<IResult> AdminDeleteLoreArticleAsync(
        Guid id,
        EditorialDbContext db,
        CancellationToken ct)
    {
        var article = await db.LoreArticles.FindAsync([id], ct);
        if (article is null) return Results.NotFound();

        db.LoreArticles.Remove(article);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    // POST /api/editorial/admin/lore/{id}/publish
    private static async Task<IResult> AdminToggleLoreArticlePublishAsync(
        Guid id,
        EditorialDbContext db,
        CancellationToken ct)
    {
        var article = await db.LoreArticles.FindAsync([id], ct);
        if (article is null) return Results.NotFound();

        if (article.Status == EditorialStatus.Draft)
        {
            article.Status      = EditorialStatus.Published;
            article.PublishedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            article.Status = EditorialStatus.Draft;
        }

        article.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.Ok(ToLoreArticleDto(article));
    }

    // ── Admin: Announcements ───────────────────────────────────────────────────────

    // GET /api/editorial/admin/announcements?page=1&pageSize=20
    private static async Task<IResult> AdminListAnnouncementsAsync(
        int? page,
        int? pageSize,
        EditorialDbContext db,
        CancellationToken ct)
    {
        var p    = Math.Max(1, page.GetValueOrDefault(1));
        var size = Math.Clamp(pageSize.GetValueOrDefault(20), 1, 100);

        var total = await db.Announcements.CountAsync(ct);

        var items = await db.Announcements
            .OrderByDescending(x => x.CreatedAt)
            .Skip((p - 1) * size)
            .Take(size)
            .Select(x => new EditorialAnnouncementDto(
                x.Id, x.Title, x.Body, x.Status.ToString(), x.PublishedAt))
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<EditorialAnnouncementDto>(items, total, p, size));
    }

    // POST /api/editorial/admin/announcements
    private static async Task<IResult> AdminCreateAnnouncementAsync(
        [FromBody] CreateAnnouncementRequest request,
        ClaimsPrincipal user,
        EditorialDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) return Results.BadRequest(new { error = "Title is required." });
        if (string.IsNullOrWhiteSpace(request.Body))  return Results.BadRequest(new { error = "Body is required." });

        var announcement = new EditorialAnnouncement
        {
            Title           = request.Title,
            Body            = request.Body,
            AuthorAccountId = GetAccountId(user),
        };

        db.Announcements.Add(announcement);
        await db.SaveChangesAsync(ct);

        var dto = ToAnnouncementDto(announcement);
        return Results.Created($"/api/editorial/admin/announcements/{announcement.Id}", dto);
    }

    // GET /api/editorial/admin/announcements/{id}
    private static async Task<IResult> AdminGetAnnouncementAsync(
        Guid id,
        EditorialDbContext db,
        CancellationToken ct)
    {
        var a = await db.Announcements.FindAsync([id], ct);
        return a is not null ? Results.Ok(ToAnnouncementDto(a)) : Results.NotFound();
    }

    // PUT /api/editorial/admin/announcements/{id}
    private static async Task<IResult> AdminUpdateAnnouncementAsync(
        Guid id,
        [FromBody] UpdateAnnouncementRequest request,
        EditorialDbContext db,
        CancellationToken ct)
    {
        var a = await db.Announcements.FindAsync([id], ct);
        if (a is null) return Results.NotFound();

        if (!Enum.TryParse<EditorialStatus>(request.Status, ignoreCase: true, out var status))
            return Results.BadRequest(new { error = "Invalid status. Use 'Draft' or 'Published'." });

        a.Title    = request.Title;
        a.Body     = request.Body;
        a.Status   = status;
        a.UpdatedAt = DateTimeOffset.UtcNow;

        if (status == EditorialStatus.Published && a.PublishedAt is null)
            a.PublishedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return Results.Ok(ToAnnouncementDto(a));
    }

    // DELETE /api/editorial/admin/announcements/{id}
    private static async Task<IResult> AdminDeleteAnnouncementAsync(
        Guid id,
        EditorialDbContext db,
        CancellationToken ct)
    {
        var a = await db.Announcements.FindAsync([id], ct);
        if (a is null) return Results.NotFound();

        db.Announcements.Remove(a);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    // POST /api/editorial/admin/announcements/{id}/publish
    private static async Task<IResult> AdminToggleAnnouncementPublishAsync(
        Guid id,
        EditorialDbContext db,
        CancellationToken ct)
    {
        var a = await db.Announcements.FindAsync([id], ct);
        if (a is null) return Results.NotFound();

        if (a.Status == EditorialStatus.Draft)
        {
            a.Status      = EditorialStatus.Published;
            a.PublishedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            a.Status = EditorialStatus.Draft;
        }

        a.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.Ok(ToAnnouncementDto(a));
    }

    // ── Helpers ────────────────────────────────────────────────────────────────────

    private static PatchNoteDto ToPatchNoteDto(PatchNote x) =>
        new(x.Id, x.Slug, x.Title, x.Summary, x.Version, x.Status.ToString(), x.PublishedAt, x.Content);

    private static LoreArticleDto ToLoreArticleDto(LoreArticle x) =>
        new(x.Id, x.Slug, x.Title, x.Summary, x.Category, x.Status.ToString(), x.PublishedAt, x.Content);

    private static EditorialAnnouncementDto ToAnnouncementDto(EditorialAnnouncement x) =>
        new(x.Id, x.Title, x.Body, x.Status.ToString(), x.PublishedAt);

    private static Guid GetAccountId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
                 ?? user.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)
                 ?? throw new InvalidOperationException("Account ID claim missing from token.");
        return Guid.Parse(value);
    }
}
