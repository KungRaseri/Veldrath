using Microsoft.EntityFrameworkCore;
using Veldrath.Contracts.Players;
using Veldrath.Server.Data;

namespace Veldrath.Server.Features.Players;

/// <summary>
/// Minimal API endpoints for public-facing player profiles.
///
/// GET /api/players/{id} — public profile of a player account.
/// </summary>
public static class PlayerEndpoints
{
    /// <summary>Registers all player profile endpoints on the provided route builder.</summary>
    public static IEndpointRouteBuilder MapPlayerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/players").WithTags("Players");

        group.MapGet("/{id:guid}", GetProfileAsync);

        return app;
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private static async Task<IResult> GetProfileAsync(Guid id, ApplicationDbContext db)
    {
        var account = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id);

        if (account is null)
            return Results.NotFound();

        var topCharacter = await db.Characters
            .AsNoTracking()
            .Where(c => c.AccountId == id && c.DeletedAt == null)
            .OrderByDescending(c => c.Level)
            .FirstOrDefaultAsync();

        return Results.Ok(new PlayerProfileDto(
            AccountId:      account.Id,
            Username:       account.UserName ?? string.Empty,
            Level:          topCharacter?.Level ?? 0,
            CharacterClass: topCharacter?.ClassName,
            Species:        topCharacter?.SpeciesSlug,
            CurrentZone:    topCharacter?.CurrentZoneId,
            RegisteredAt:   account.CreatedAt));
    }
}
