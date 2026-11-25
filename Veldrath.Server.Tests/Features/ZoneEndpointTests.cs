using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class ZoneEndpointTests(WebAppFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    /// <summary>Seeds a live session for a character inside an instance run in <paramref name="zoneId"/>.</summary>
    /// <param name="characterName">Character to create and place in the run.</param>
    /// <param name="zoneId">Zone the run belongs to.</param>
    /// <returns>The created character id, for <see cref="RemoveRunMemberAsync"/>.</returns>
    private async Task<Guid> SeedRunMemberAsync(string characterName, string zoneId = "crestfall")
    {
        var (_, accountId) = await factory.CreateAccountTokenAsync($"zonecount_{characterName}");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var character = new Character
        {
            AccountId = accountId,
            Name = characterName,
            ClassName = "Warrior",
            SlotIndex = 1,
            Attributes = "{}",
        };
        db.Characters.Add(character);

        var run = new ZoneInstance
        {
            ZoneId = zoneId,
            CreatedByCharacterId = character.Id,
            ZoneGroup = $"{zoneId}#i1:{Guid.NewGuid():D}",
        };
        db.ZoneInstances.Add(run);

        db.PlayerSessions.Add(new PlayerSession
        {
            CharacterId = character.Id,
            CharacterName = characterName,
            ConnectionId = $"conn-{Guid.NewGuid():N}",
            RegionId = "varenmark",
            ZoneId = zoneId,
            ZoneInstanceId = run.Id,
        });
        await db.SaveChangesAsync();
        return character.Id;
    }

    /// <summary>Removes the run and session <see cref="SeedRunMemberAsync"/> created.</summary>
    /// <param name="characterId">Character id returned by the seeder.</param>
    private async Task RemoveRunMemberAsync(Guid characterId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.PlayerSessions.RemoveRange(await db.PlayerSessions.Where(s => s.CharacterId == characterId).ToListAsync());
        db.ZoneInstances.RemoveRange(
            await db.ZoneInstances.Where(r => r.CreatedByCharacterId == characterId).ToListAsync());
        if (await db.Characters.FindAsync(characterId) is { } character)
            db.Characters.Remove(character);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetZones_Should_Return_All_Seeded_Zones()
    {
        var response = await _client.GetAsync("/api/zones");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var zones = await response.Content.ReadFromJsonAsync<ZoneDto[]>();
        zones.Should().NotBeNull();
        zones!.Length.Should().Be(18);
    }

    [Fact]
    public async Task GetZones_Should_Include_Starter_Zone()
    {
        var response = await _client.GetAsync("/api/zones");
        var zones = await response.Content.ReadFromJsonAsync<ZoneDto[]>();

        var starter = zones!.SingleOrDefault(z => z.IsStarter);
        starter.Should().NotBeNull();
        starter!.Id.Should().Be("crestfall");
    }

    [Fact]
    public async Task GetZones_Should_Return_Correct_Zone_Types()
    {
        var zones = await _client.GetFromJsonAsync<ZoneDto[]>("/api/zones");
        zones.Should().Contain(z => z.Type == "Town");
        zones.Should().Contain(z => z.Type == "Dungeon");
        zones.Should().Contain(z => z.Type == "Wilderness");
    }

    [Fact]
    public async Task GetZones_Should_Return_Zero_Online_Players_Initially()
    {
        var zones = await _client.GetFromJsonAsync<ZoneDto[]>("/api/zones");
        zones!.All(z => z.OnlinePlayers == 0).Should().BeTrue();
    }

    [Fact]
    public async Task GetZoneById_Counts_Characters_Inside_An_Instance_Run()
    {
        // The public catalog says how busy a zone is, not who can see whom: the count is the whole zone's
        // population, so a party mid-delve still counts as online in the zone it is delving in. Scoping a
        // roster to the caller's group is the hub's job (EnterZone / the arrival snapshot), not this endpoint's.
        var before = (await _client.GetFromJsonAsync<ZoneDto>("/api/zones/crestfall"))!.OnlinePlayers;
        var characterId = await SeedRunMemberAsync($"ZoneCount_{Guid.NewGuid():N}");

        try
        {
            var zone = await _client.GetFromJsonAsync<ZoneDto>("/api/zones/crestfall");

            zone!.OnlinePlayers.Should().Be(
                before + 1, "the published count is the zone's whole population, run members included");
        }
        finally
        {
            await RemoveRunMemberAsync(characterId);
        }
    }

    [Fact]
    public async Task GetZoneById_Should_Return_Zone_Details()
    {
        var response = await _client.GetAsync("/api/zones/crestfall");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var zone = await response.Content.ReadFromJsonAsync<ZoneDto>();
        zone!.Id.Should().Be("crestfall");
        zone.IsStarter.Should().BeTrue();
        zone.MinLevel.Should().Be(0);
    }

    [Fact]
    public async Task GetZoneById_Should_Return_404_For_Unknown_Zone()
    {
        var response = await _client.GetAsync("/api/zones/nonexistent-zone");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetZones_Does_Not_Require_Authentication()
    {
        // Zones list should be publicly accessible (let players see server world without signing in)
        using var anonClient = factory.CreateClient();
        var response = await anonClient.GetAsync("/api/zones");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetZoneById_Should_Return_404_For_DevOnly_Zone()
    {
        // Dev-only zones must never be reachable through the public API
        var response = await _client.GetAsync("/api/zones/playground");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetZoneLocations_Should_Return_404_For_DevOnly_Zone()
    {
        // Location list for a dev zone must also be blocked from the public API
        var response = await _client.GetAsync("/api/zones/playground/locations");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
