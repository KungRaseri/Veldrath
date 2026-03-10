using RealmUnbound.Server.Data.Entities;
using RealmUnbound.Server.Data.Repositories;

namespace RealmUnbound.Server.Features.Zones;

// ── DTOs ──────────────────────────────────────────────────────────────────────
public record ZoneDto(
    string Id,
    string Name,
    string Description,
    string Type,
    int MinLevel,
    int MaxPlayers,
    bool IsStarter,
    int OnlinePlayers);

// ── Endpoint map ──────────────────────────────────────────────────────────────
public static class ZoneEndpoints
{
    public static IEndpointRouteBuilder MapZoneEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/zones").WithTags("zones");

        /// <summary>Returns all available zones with current online player counts.</summary>
        group.MapGet("/", async (IZoneRepository zones, IZoneSessionRepository sessions) =>
        {
            var all = await zones.GetAllAsync();
            var counts = new Dictionary<string, int>();
            foreach (var z in all)
                counts[z.Id] = (await sessions.GetByZoneIdAsync(z.Id)).Count;

            return Results.Ok(all.Select(z => new ZoneDto(
                z.Id, z.Name, z.Description,
                z.Type.ToString(), z.MinLevel, z.MaxPlayers,
                z.IsStarter, counts.GetValueOrDefault(z.Id))));
        });

        /// <summary>Returns details of a single zone.</summary>
        group.MapGet("/{id}", async (string id, IZoneRepository zones, IZoneSessionRepository sessions) =>
        {
            var zone = await zones.GetByIdAsync(id);
            if (zone is null) return Results.NotFound();

            var count = (await sessions.GetByZoneIdAsync(id)).Count;
            return Results.Ok(new ZoneDto(
                zone.Id, zone.Name, zone.Description,
                zone.Type.ToString(), zone.MinLevel, zone.MaxPlayers,
                zone.IsStarter, count));
        });

        return app;
    }
}
