using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Features.Zones;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Covers the instance-run lifecycle against a real SQLite schema, so the two partial unique indexes that
/// make the entry race safe are actually enforced rather than mocked away.
/// </summary>
public class ZoneInstanceServiceTests
{
    private const string ZoneId = "the-halrow";

    private static async Task<Guid> SeedAccountAsync(ApplicationDbContext db)
    {
        var name = $"user_{Guid.NewGuid():N}";
        var account = new PlayerAccount { UserName = name, NormalizedUserName = name.ToUpperInvariant() };
        db.Users.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    private static async Task<Character> SeedCharacterAsync(ApplicationDbContext db, Guid accountId, string name)
    {
        var character = new Character
        {
            AccountId = accountId,
            Name = name,
            Level = 5,
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return character;
    }

    private static async Task<Party> SeedPartyAsync(ApplicationDbContext db, params Guid[] memberIds)
    {
        var party = new Party { LeaderCharacterId = memberIds[0] };
        db.Parties.Add(party);

        foreach (var memberId in memberIds)
        {
            db.PartyMembers.Add(new PartyMemberEntity { PartyId = party.Id, CharacterId = memberId });
        }

        await db.SaveChangesAsync();
        return party;
    }

    private static EfCoreZoneInstanceService Service(ApplicationDbContext db) =>
        new(db, NullLogger<EfCoreZoneInstanceService>.Instance);

    [Fact]
    public async Task FindOrCreate_GivesEveryPartyMemberTheSameRun()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var accountId = await SeedAccountAsync(db);
        var leader = await SeedCharacterAsync(db, accountId, "Thornwarden");
        var second = await SeedCharacterAsync(db, await SeedAccountAsync(db), "Mirelle");
        await SeedPartyAsync(db, leader.Id, second.Id);

        var service = Service(db);
        var first = await service.FindOrCreateAsync(ZoneId, leader.Id);
        var again = await service.FindOrCreateAsync(ZoneId, second.Id);

        first.Id.Should().Be(again.Id);
        first.ZoneGroup.Should().Be($"zone:{ZoneId}#i1:{first.Id:D}");
        (await db.ZoneInstances.CountAsync()).Should().Be(1);
        (await db.ZoneInstanceParticipants.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task FindOrCreate_ReturnsTheSameRunOnASecondVisit()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var accountId = await SeedAccountAsync(db);
        var leader = await SeedCharacterAsync(db, accountId, "Thornwarden");
        var second = await SeedCharacterAsync(db, await SeedAccountAsync(db), "Mirelle");
        await SeedPartyAsync(db, leader.Id, second.Id);

        var service = Service(db);
        var first = await service.FindOrCreateAsync(ZoneId, leader.Id);
        var again = await service.FindOrCreateAsync(ZoneId, leader.Id);

        again.Id.Should().Be(first.Id);
        (await db.ZoneInstances.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task FindOrCreate_KeepsSoloCharactersApart()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var solo = await SeedCharacterAsync(db, await SeedAccountAsync(db), "Lonewalker");
        var stranger = await SeedCharacterAsync(db, await SeedAccountAsync(db), "Passerby");

        var service = Service(db);
        var mine = await service.FindOrCreateAsync(ZoneId, solo.Id);
        var theirs = await service.FindOrCreateAsync(ZoneId, stranger.Id);

        mine.Id.Should().NotBe(theirs.Id);
        mine.OwnerPartyId.Should().BeNull();
        mine.CreatedByCharacterId.Should().Be(solo.Id);
        (await db.ZoneInstances.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task FindOrCreate_KeepsDifferentZonesApart()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var solo = await SeedCharacterAsync(db, await SeedAccountAsync(db), "Lonewalker");

        var service = Service(db);
        var halrow = await service.FindOrCreateAsync(ZoneId, solo.Id);
        var pits = await service.FindOrCreateAsync("drowning-pits", solo.Id);

        halrow.Id.Should().NotBe(pits.Id);
        halrow.ZoneId.Should().Be(ZoneId);
        pits.ZoneId.Should().Be("drowning-pits");
    }

    [Fact]
    public async Task Leave_ClosesParticipation_AndJoinReopensIt()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var solo = await SeedCharacterAsync(db, await SeedAccountAsync(db), "Lonewalker");

        var service = Service(db);
        var run = await service.FindOrCreateAsync(ZoneId, solo.Id);
        await service.LeaveAsync(run.Id, solo.Id);

        var left = await db.ZoneInstanceParticipants
            .SingleAsync(row => row.ZoneInstanceId == run.Id && row.CharacterId == solo.Id);
        left.LeftAt.Should().NotBeNull();

        (await service.JoinAsync(run.Id, solo.Id)).Should().BeTrue();

        var back = await db.ZoneInstanceParticipants
            .SingleAsync(row => row.ZoneInstanceId == run.Id && row.CharacterId == solo.Id);
        back.LeftAt.Should().BeNull();
    }

    [Fact]
    public async Task Join_RefusesARunThatIsBeingPurged()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var solo = await SeedCharacterAsync(db, await SeedAccountAsync(db), "Lonewalker");

        var service = Service(db);
        var run = await service.FindOrCreateAsync(ZoneId, solo.Id);
        run.State = ZoneInstanceState.Purging;
        await db.SaveChangesAsync();

        (await service.JoinAsync(run.Id, solo.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Sweep_PurgesARunNobodyIsInside_AndKeepsAnOccupiedOne()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var solo = await SeedCharacterAsync(db, await SeedAccountAsync(db), "Lonewalker");
        var other = await SeedCharacterAsync(db, await SeedAccountAsync(db), "Passerby");

        var service = Service(db);
        var abandoned = await service.FindOrCreateAsync(ZoneId, solo.Id);
        var occupied = await service.FindOrCreateAsync("drowning-pits", other.Id);

        // Nobody is inside either run yet, so the abandoned one is stale and the other is held open by a session.
        abandoned.LastOccupiedAt = DateTimeOffset.UtcNow.AddHours(-1);
        occupied.LastOccupiedAt = DateTimeOffset.UtcNow.AddHours(-1);
        db.PlayerSessions.Add(new PlayerSession
        {
            CharacterId = other.Id,
            CharacterName = "Passerby",
            ConnectionId = "conn-1",
            RegionId = "varenmark",
            ZoneId = "drowning-pits",
            ZoneInstanceId = occupied.Id,
        });
        await db.SaveChangesAsync();

        // The first sweep marks the abandoned run empty; the second deletes it.
        await service.SweepAsync(TimeSpan.FromMinutes(5));
        await service.SweepAsync(TimeSpan.FromMinutes(5));

        var remaining = await db.ZoneInstances.Select(row => row.Id).ToListAsync();
        remaining.Should().Contain(occupied.Id);
        remaining.Should().NotContain(abandoned.Id);
    }

    [Fact]
    public async Task Sweep_KeepsARunInsideTheGraceWindow()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var solo = await SeedCharacterAsync(db, await SeedAccountAsync(db), "Lonewalker");

        var service = Service(db);
        var fresh = await service.FindOrCreateAsync(ZoneId, solo.Id);

        await service.SweepAsync(TimeSpan.FromMinutes(5));

        (await db.ZoneInstances.AnyAsync(row => row.Id == fresh.Id)).Should().BeTrue();
    }
}
