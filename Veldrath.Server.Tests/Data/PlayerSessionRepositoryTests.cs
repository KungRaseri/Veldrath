using Microsoft.EntityFrameworkCore;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Data;

/// <summary>
/// Tests for the session-lookup and map-position half of <see cref="PlayerSessionRepository"/>: the region
/// population queries and the in-place map/node/zone update used when a player travels on the region map.
/// </summary>
public class PlayerSessionRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static async Task<Character> SeedCharacterAsync(ApplicationDbContext db, string name)
    {
        var account = new PlayerAccount { UserName = $"acct_{Guid.NewGuid():N}" };
        account.NormalizedUserName = account.UserName.ToUpperInvariant();
        db.Users.Add(account);
        var character = new Character
        {
            AccountId = account.Id,
            Name = name,
            ClassName = "Warrior",
            SlotIndex = 1,
        };
        db.Characters.Add(character);
        if (!await db.Worlds.AnyAsync(world => world.Id == "world-1"))
            db.Worlds.Add(new World { Id = "world-1", Name = "Test world" });
        if (!await db.Regions.AnyAsync(region => region.Id == "region-1"))
            db.Regions.Add(new Region { Id = "region-1", Name = "Test region", WorldId = "world-1" });
        await db.SaveChangesAsync();
        return character;
    }

    private static PlayerSession Session(Character character, string connectionId, string? zoneId = null) =>
        new()
        {
            CharacterId = character.Id,
            CharacterName = character.Name,
            ConnectionId = connectionId,
            RegionId = "region-1",
            ZoneId = zoneId,
        };

    [Fact]
    public async Task GetByRegionIdAsync_ReturnsEverySessionOnTheRegion()
    {
        await using var db = _factory.CreateContext();
        var first = await SeedCharacterAsync(db, "Aria");
        var second = await SeedCharacterAsync(db, "Bran");
        db.PlayerSessions.AddRange(Session(first, "conn-1", "crestfall"), Session(second, "conn-2"));
        await db.SaveChangesAsync();
        var repo = new PlayerSessionRepository(db);

        var result = await repo.GetByRegionIdAsync("region-1");

        result.Should().HaveCount(2);
        result.Select(s => s.ConnectionId).Should().BeEquivalentTo(["conn-1", "conn-2"]);
    }

    [Fact]
    public async Task GetOnRegionMapAsync_ReturnsOnlySessionsOutsideAZone()
    {
        await using var db = _factory.CreateContext();
        var first = await SeedCharacterAsync(db, "Aria");
        var second = await SeedCharacterAsync(db, "Bran");
        db.PlayerSessions.AddRange(Session(first, "conn-map"), Session(second, "conn-zone", "crestfall"));
        await db.SaveChangesAsync();
        var repo = new PlayerSessionRepository(db);

        var result = await repo.GetOnRegionMapAsync("region-1");

        result.Should().ContainSingle().Which.ConnectionId.Should().Be("conn-map");
    }

    [Fact]
    public async Task GetOnSharedScopeAsync_ReadsTheSharedScopeWhileTheZoneCountStillSeesRunMembers()
    {
        await using var db = _factory.CreateContext();
        var resident = await SeedCharacterAsync(db, "Aria");
        var delver = await SeedCharacterAsync(db, "Bran");
        var elsewhere = await SeedCharacterAsync(db, "Cass");

        var delverSession = Session(delver, "conn-delving", "crestfall");
        delverSession.ZoneInstanceId = Guid.NewGuid();
        db.PlayerSessions.AddRange(
            Session(resident, "conn-shared", "crestfall"),
            delverSession,
            Session(elsewhere, "conn-other-zone", "the-droveway"));
        await db.SaveChangesAsync();
        var repo = new PlayerSessionRepository(db);

        var shared = await repo.GetOnSharedScopeAsync("crestfall");
        var wholeZone = await repo.GetByZoneIdAsync("crestfall");

        shared.Should().ContainSingle().Which.ConnectionId.Should().Be(
            "conn-shared", "a character inside a run is in another SignalR group, so not in the shared roster");
        wholeZone.Select(s => s.ConnectionId).Should().BeEquivalentTo(
            ["conn-shared", "conn-delving"],
            "the public zone count publishes the whole zone's population, run members included");
    }

    [Fact]
    public async Task UpdateMapPositionAsync_MovesTheSessionOntoTheNodeAndZone()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedCharacterAsync(db, "Aria");
        db.PlayerSessions.Add(Session(character, "conn-1"));
        await db.SaveChangesAsync();
        var repo = new PlayerSessionRepository(db);

        await repo.UpdateMapPositionAsync(character.Id, "woods", "clearing", "crestfall", "varenmark");

        var session = await repo.GetByCharacterIdAsync(character.Id);
        session.Should().NotBeNull();
        session!.CurrentMapId.Should().Be("woods");
        session.CurrentNodeId.Should().Be("clearing");
        session.ZoneId.Should().Be("crestfall");
        session.RegionId.Should().Be("varenmark");
    }

    [Fact]
    public async Task UpdateMapPositionAsync_KeepsTheRegion_WhenNoneIsSupplied()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedCharacterAsync(db, "Aria");
        db.PlayerSessions.Add(Session(character, "conn-1"));
        await db.SaveChangesAsync();
        var repo = new PlayerSessionRepository(db);

        await repo.UpdateMapPositionAsync(character.Id, "woods", "clearing", zoneId: null, regionId: "  ");

        var session = await repo.GetByCharacterIdAsync(character.Id);
        session!.RegionId.Should().Be("region-1", "a blank region must not blank the player's region");
        session.ZoneId.Should().BeNull();
        session.CurrentNodeId.Should().Be("clearing");
    }

    [Fact]
    public async Task UpdateMapPositionAsync_DoesNothing_WhenTheSessionIsGone()
    {
        await using var db = _factory.CreateContext();
        await SeedCharacterAsync(db, "Aria");
        var repo = new PlayerSessionRepository(db);

        await repo.UpdateMapPositionAsync(Guid.NewGuid(), "woods", "clearing", "crestfall", "region-1");

        db.PlayerSessions.Should().BeEmpty();
    }
}
