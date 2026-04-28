using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Contracts.Content;
using Veldrath.Contracts.Zones;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Features.Dev;

/// <summary>Minimal-API endpoint registrations for developer-only tooling.</summary>
/// <remarks>
/// All endpoints in this group require authentication and return <c>403 Forbidden</c>
/// when the server is not running in the <c>Development</c> or <c>Test</c> environment,
/// making them safe to deploy: they exist in production but are unreachable.
/// </remarks>
public static class DevEndpoints
{
    /// <summary>Maps all developer tooling endpoints onto <paramref name="app"/>.</summary>
    public static IEndpointRouteBuilder MapDevEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dev")
            .WithTags("dev")
            .RequireAuthorization();

        // Returns all zones, including dev-only sandbox zones hidden from the public API.
        group.MapGet("/zones", async (
            IZoneRepository zones,
            IWebHostEnvironment env) =>
        {
            if (!IsDevEnvironment(env))
                return Results.Problem(
                    title: "Dev endpoints are not available.",
                    statusCode: StatusCodes.Status403Forbidden);

            var all = await zones.GetAllIncludingDevAsync();
            return Results.Ok(all.Select(z => new ZoneDto(
                z.Id, z.Name, z.Description, z.Type.ToString(),
                z.MinLevel, z.MaxPlayers, z.IsStarter,
                OnlinePlayers: 0, z.RegionId, z.HasInn, z.HasMerchant)));
        });

        // Returns details of a single zone, including dev-only zones not visible in the public API.
        group.MapGet("/zones/{id}", async (
            string id,
            ApplicationDbContext db,
            IWebHostEnvironment env) =>
        {
            if (!IsDevEnvironment(env))
                return Results.Problem(
                    title: "Dev endpoints are not available.",
                    statusCode: StatusCodes.Status403Forbidden);

            var zone = await db.Zones.FirstOrDefaultAsync(z => z.Id == id);
            if (zone is null)
                return Results.NotFound(new { Message = $"Zone '{id}' not found." });

            return Results.Ok(new ZoneDto(zone.Id, zone.Name, zone.Description, zone.Type.ToString(),
                zone.MinLevel, zone.MaxPlayers, zone.IsStarter, OnlinePlayers: 0,
                zone.RegionId, zone.HasInn, zone.HasMerchant));
        });

        // Returns ALL locations in a zone, including hidden ones. For dev inspection only.
        group.MapGet("/zones/{id}/locations", async (
            string id,
            ApplicationDbContext db,
            IZoneLocationRepository locationRepo,
            IWebHostEnvironment env) =>
        {
            if (!IsDevEnvironment(env))
                return Results.Problem(
                    title: "Dev endpoints are not available.",
                    statusCode: StatusCodes.Status403Forbidden);

            var zone = await db.Zones.FirstOrDefaultAsync(z => z.Id == id);
            if (zone is null)
                return Results.NotFound(new { Message = $"Zone '{id}' not found." });

            var locations = await locationRepo.GetAllByZoneIdAsync(id);
            return Results.Ok(locations.Select(ToLocationDto));
        });

        // Teleports the given character to the given zone by updating CurrentZoneId.
        group.MapPost("/teleport", async (
            TeleportRequest request,
            ApplicationDbContext db,
            IZoneLocationRepository locationRepo,
            IWebHostEnvironment env) =>
        {
            if (!IsDevEnvironment(env))
                return Results.Problem(
                    title: "Dev endpoints are not available.",
                    statusCode: StatusCodes.Status403Forbidden);

            var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == request.CharacterId);
            if (character is null)
                return Results.NotFound(new { Message = $"Character '{request.CharacterId}' not found." });

            var zone = await db.Zones.FirstOrDefaultAsync(z => z.Id == request.ZoneId);
            if (zone is null)
                return Results.NotFound(new { Message = $"Zone '{request.ZoneId}' not found." });

            character.CurrentZoneId = zone.Id;

            if (request.LocationSlug is not null)
            {
                var location = await locationRepo.GetBySlugAsync(request.LocationSlug);
                if (location is null || !location.ZoneId.Equals(zone.Id, StringComparison.OrdinalIgnoreCase))
                    return Results.NotFound(new { Message = $"Location '{request.LocationSlug}' not found in zone '{zone.Id}'." });
                character.CurrentZoneLocationSlug = location.Slug;
            }
            else
            {
                character.CurrentZoneLocationSlug = null; // clear location within zone on teleport
            }

            character.TileZoneId = null; // force tile-snap on next zone entry
            await db.SaveChangesAsync();

            return Results.Ok(new TeleportResult(character.Id, zone.Id, zone.Name, character.CurrentZoneLocationSlug));
        });

        return app;
    }

    private static ZoneLocationDto ToLocationDto(ZoneLocationEntry e) =>
        new(e.Slug, e.DisplayName, e.TypeKey, e.ZoneId, e.RarityWeight, e.MinLevel, e.MaxLevel, e.IsHidden);

    private static bool IsDevEnvironment(IWebHostEnvironment env) =>
        env.IsDevelopment() || env.IsEnvironment("Test");
}

/// <summary>Request body for the <c>POST /api/dev/teleport</c> endpoint.</summary>
/// <param name="CharacterId">The unique identifier of the character to teleport.</param>
/// <param name="ZoneId">The slug of the destination zone.</param>
/// <param name="LocationSlug">Optional slug of a specific location within the zone to land on.</param>
public record TeleportRequest(Guid CharacterId, string ZoneId, string? LocationSlug = null);

/// <summary>Response body returned by the <c>POST /api/dev/teleport</c> endpoint.</summary>
/// <param name="CharacterId">The character that was teleported.</param>
/// <param name="ZoneId">The zone the character was moved to.</param>
/// <param name="ZoneName">The display name of the destination zone.</param>
/// <param name="LocationSlug">The specific location within the zone, or <see langword="null"/> if no location was requested.</param>
public record TeleportResult(Guid CharacterId, string ZoneId, string ZoneName, string? LocationSlug = null);
