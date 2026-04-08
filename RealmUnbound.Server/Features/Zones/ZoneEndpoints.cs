using RealmEngine.Shared.Abstractions;
using RealmUnbound.Contracts.Content;
using RealmUnbound.Contracts.Zones;
using RealmUnbound.Server.Data.Entities;
using RealmUnbound.Server.Data.Repositories;

namespace RealmUnbound.Server.Features.Zones;

// Endpoint map
/// <summary>Minimal-API endpoint registrations for zone, region, and world catalog queries.</summary>
public static class ZoneEndpoints
{
    /// <summary>Maps all zone, region, and world endpoints onto <paramref name="app"/>.</summary>
    public static IEndpointRouteBuilder MapZoneEndpoints(this IEndpointRouteBuilder app)
    {
        MapZones(app);
        MapRegions(app);
        MapWorlds(app);
        return app;
    }

    private static void MapZones(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/zones").WithTags("zones");

        // Returns all available zones with current online player counts.
        group.MapGet("/", async (IZoneRepository zones, IZoneSessionRepository sessions) =>
        {
            var all = await zones.GetAllAsync();
            var counts = new Dictionary<string, int>();
            foreach (var z in all)
                counts[z.Id] = (await sessions.GetByZoneIdAsync(z.Id)).Count;

            return Results.Ok(all.Select(z => ToDto(z, counts.GetValueOrDefault(z.Id))));
        });

        // Returns details of a single zone.
        group.MapGet("/{id}", async (string id, IZoneRepository zones, IZoneSessionRepository sessions) =>
        {
            var zone = await zones.GetByIdAsync(id);
            if (zone is null) return Results.NotFound();

            var count = (await sessions.GetByZoneIdAsync(id)).Count;
            return Results.Ok(ToDto(zone, count));
        });

        // Returns all zones belonging to the given region.
        group.MapGet("/by-region/{regionId}", async (string regionId, IZoneRepository zones, IZoneSessionRepository sessions) =>
        {
            var all = await zones.GetByRegionIdAsync(regionId);
            var counts = new Dictionary<string, int>();
            foreach (var z in all)
                counts[z.Id] = (await sessions.GetByZoneIdAsync(z.Id)).Count;

            return Results.Ok(all.Select(z => ToDto(z, counts.GetValueOrDefault(z.Id))));
        });

        // Returns all zone locations within a specific zone, filtered by character unlock state when characterId is supplied.
        group.MapGet("/{id}/locations", async (
            string id,
            Guid? characterId,
            IZoneLocationRepository locations,
            ICharacterUnlockedLocationRepository? unlockedRepo) =>
        {
            if (characterId.HasValue && unlockedRepo is not null)
            {
                var unlocked = await unlockedRepo.GetUnlockedSlugsAsync(characterId.Value);
                return Results.Ok((await locations.GetByZoneIdAsync(id, unlocked)).Select(ToLocationDto));
            }
            return Results.Ok((await locations.GetByZoneIdAsync(id)).Select(ToLocationDto));
        });
    }

    private static void MapRegions(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/regions").WithTags("regions");

        // Returns all regions ordered by minimum level.
        group.MapGet("/", async (IRegionRepository regions) =>
            Results.Ok((await regions.GetAllAsync()).Select(ToDto)));

        // Returns a single region by slug.
        group.MapGet("/{id}", async (string id, IRegionRepository regions) =>
        {
            var region = await regions.GetByIdAsync(id);
            return region is null ? Results.NotFound() : Results.Ok(ToDto(region));
        });

        // Returns all regions reachable from the given region.
        group.MapGet("/{id}/connections", async (string id, IRegionRepository regions) =>
            Results.Ok((await regions.GetConnectedAsync(id)).Select(ToDto)));
    }

    private static void MapWorlds(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/worlds").WithTags("worlds");

        // Returns all worlds (currently only Draveth).
        group.MapGet("/", async (IWorldRepository worlds) =>
            Results.Ok((await worlds.GetAllAsync()).Select(w => new WorldDto(w.Id, w.Name, w.Description, w.Era))));

        // Returns a single world by slug.
        group.MapGet("/{id}", async (string id, IWorldRepository worlds) =>
        {
            var world = await worlds.GetByIdAsync(id);
            return world is null ? Results.NotFound() : Results.Ok(new WorldDto(world.Id, world.Name, world.Description, world.Era));
        });
    }

    private static ZoneDto ToDto(Zone z, int onlinePlayers = 0) =>
        new(z.Id, z.Name, z.Description, z.Type.ToString(), z.MinLevel, z.MaxPlayers,
            z.IsStarter, onlinePlayers, z.RegionId, z.HasInn, z.HasMerchant);

    private static RegionDto ToDto(Region r) =>
        new(r.Id, r.Name, r.Description, r.Type.ToString(), r.MinLevel, r.MaxLevel, r.IsStarter, r.WorldId);

    private static ZoneLocationDto ToLocationDto(RealmEngine.Shared.Models.ZoneLocationEntry e) =>
        new(e.Slug, e.DisplayName, e.TypeKey, e.ZoneId, e.RarityWeight, e.MinLevel, e.MaxLevel, e.IsHidden);
}
