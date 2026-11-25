using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
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

    private static EfCoreZoneInstanceService Service(
        ApplicationDbContext db,
        IReadOnlyList<RealmEngine.Shared.Models.ZoneLocationEntry>? hidden = null,
        RealmEngine.Shared.Models.ZoneLocationEntry? resumable = null)
    {
        var locations = new Mock<RealmEngine.Shared.Abstractions.IZoneLocationRepository>();
        locations
            .Setup(repo => repo.GetHiddenByZoneIdAsync(It.IsAny<string>()))
            .ReturnsAsync((hidden ?? []).ToList());
        locations
            .Setup(repo => repo.GetBySlugAsync(It.IsAny<string>()))
            .ReturnsAsync(resumable);

        return new(
            db,
            new CharacterRepository(db),
            new CharacterUnlockedLocationRepository(db),
            locations.Object,
            NullLogger<EfCoreZoneInstanceService>.Instance);
    }

    private const string VaultSlug = "ancestor-vault";

    private static RealmEngine.Shared.Models.ZoneLocationEntry Room(
        string slug = VaultSlug,
        string zoneId = ZoneId,
        bool isInstanced = true) =>
        new(slug, slug, "dungeons", zoneId, 30, 11, 14, IsInstanced: isInstanced);

    private static RealmEngine.Shared.Models.ZoneLocationEntry HiddenRoom(string slug, string unlockKey) =>
        new(
            slug,
            slug,
            "dungeons",
            ZoneId,
            30,
            11,
            14,
            IsHidden: true,
            UnlockType: "quest",
            UnlockKey: unlockKey);

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
    public async Task Join_RefusesARunThatHasEmptied()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var solo = await SeedCharacterAsync(db, await SeedAccountAsync(db), "Lonewalker");

        var service = Service(db);
        var run = await service.FindOrCreateAsync(ZoneId, solo.Id);
        run.State = ZoneInstanceState.Empty;
        await db.SaveChangesAsync();

        (await service.JoinAsync(run.Id, solo.Id)).Should().BeFalse(
            "an emptied run is waiting for the next sweep to delete it, so the join must not report success");
    }

    [Fact]
    public async Task Leave_StartsTheDisconnectGraceAtTheDeparture()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var solo = await SeedCharacterAsync(db, await SeedAccountAsync(db), "Lonewalker");

        var service = Service(db, resumable: Room());
        var run = await service.FindOrCreateAsync(ZoneId, solo.Id);

        // The run was held for an hour. Measuring the grace from the last join would sweep it on the first tick
        // after the sole member disconnects, so the reconnect would find an empty run and no resume.
        run.LastOccupiedAt = DateTimeOffset.UtcNow.AddHours(-1);
        await db.SaveChangesAsync();

        await service.LeaveAsync(run.Id, solo.Id);
        await service.SweepAsync(TimeSpan.FromMinutes(1));

        var resumed = await service.FindResumableAsync(ZoneId, VaultSlug, solo.Id);
        resumed.Should().NotBeNull("the grace starts at the departure, so a relog inside it still rejoins the run");
        resumed!.Id.Should().Be(run.Id);

        // Once the grace itself has passed with nobody back, the run empties and the resume falls away.
        run.LastOccupiedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        await db.SaveChangesAsync();
        await service.SweepAsync(TimeSpan.FromMinutes(1));

        (await service.FindResumableAsync(ZoneId, VaultSlug, solo.Id)).Should().BeNull(
            "a run that stayed empty past the grace is waiting to be purged, not to be resumed");
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

    [Fact]
    public async Task RecordClear_MarksTheRunAndCreditsTheCharacterOnce()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var solo = await SeedCharacterAsync(db, await SeedAccountAsync(db), "Lonewalker");

        var service = Service(db);
        var run = await service.FindOrCreateAsync(ZoneId, solo.Id);

        var first = await service.RecordClearAsync(run.Id, "ancestor-vault", solo.Id);
        first.Cleared.Should().BeTrue();
        first.FirstClearOfRun.Should().BeTrue();
        first.FirstClearForCharacter.Should().BeTrue();

        var again = await service.RecordClearAsync(run.Id, "ancestor-vault", solo.Id);
        again.Cleared.Should().BeTrue();
        again.FirstClearOfRun.Should().BeFalse();
        again.FirstClearForCharacter.Should().BeFalse();

        (await service.GetClearedLocationsAsync(run.Id)).Should().Equal("ancestor-vault");

        // The character carries the first-clear permanently, so F4 can tell "done" from "someone did it".
        var character = await db.Characters.SingleAsync(row => row.Id == solo.Id);
        character.Attributes.Should().Contain("FirstClear_ancestor-vault");
    }

    [Fact]
    public async Task GetClearedLocations_ListsTheRoomsInClearOrder()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var solo = await SeedCharacterAsync(db, await SeedAccountAsync(db), "Lonewalker");

        var service = Service(db);
        var run = await service.FindOrCreateAsync(ZoneId, solo.Id);

        await service.RecordClearAsync(run.Id, "deeps-entrance", solo.Id);
        await service.RecordClearAsync(run.Id, "ancestor-vault", solo.Id);

        (await service.GetClearedLocationsAsync(run.Id))
            .Should().Equal("deeps-entrance", "ancestor-vault");
    }

    [Fact]
    public async Task RecordClear_GrantsTheHiddenRoomThatNamesItAsItsKey()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var solo = await SeedCharacterAsync(db, await SeedAccountAsync(db), "Lonewalker");

        var service = Service(db, [HiddenRoom("relic-chamber", "ancestor-vault")]);
        var run = await service.FindOrCreateAsync(ZoneId, solo.Id);

        var outcome = await service.RecordClearAsync(run.Id, "ancestor-vault", solo.Id);

        outcome.UnlockedLocationSlugs.Should().ContainSingle().Which.Should().Be("relic-chamber");
        (await db.CharacterUnlockedLocations
            .AnyAsync(row => row.CharacterId == solo.Id && row.LocationSlug == "relic-chamber"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task RecordClear_ForAnUnknownRun_ReportsNothing()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var solo = await SeedCharacterAsync(db, await SeedAccountAsync(db), "Lonewalker");

        var outcome = await Service(db).RecordClearAsync(Guid.NewGuid(), "ancestor-vault", solo.Id);

        outcome.Cleared.Should().BeFalse();
        outcome.UnlockedLocationSlugs.Should().BeEmpty();
    }

    // ── Resume ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task FindResumable_ReturnsThePartysOpenRun()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var leader = await SeedCharacterAsync(db, await SeedAccountAsync(db), "Thornwarden");
        var second = await SeedCharacterAsync(db, await SeedAccountAsync(db), "Mirelle");
        await SeedPartyAsync(db, leader.Id, second.Id);

        var run = await Service(db).FindOrCreateAsync(ZoneId, leader.Id);

        var resumed = await Service(db, resumable: Room()).FindResumableAsync(ZoneId, VaultSlug, second.Id);

        resumed.Should().NotBeNull();
        resumed!.Id.Should().Be(run.Id);
    }

    [Fact]
    public async Task FindResumable_ReturnsTheSoloRun()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var solo = await SeedCharacterAsync(db, await SeedAccountAsync(db), "Lonewalker");

        var run = await Service(db).FindOrCreateAsync(ZoneId, solo.Id);

        var resumed = await Service(db, resumable: Room()).FindResumableAsync(ZoneId, VaultSlug, solo.Id);

        resumed.Should().NotBeNull();
        resumed!.Id.Should().Be(run.Id);
    }

    [Fact]
    public async Task FindResumable_IgnoresAnotherPartysRun()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var theirs = await SeedCharacterAsync(db, await SeedAccountAsync(db), "Thornwarden");
        var mine = await SeedCharacterAsync(db, await SeedAccountAsync(db), "Mirelle");
        await SeedPartyAsync(db, theirs.Id);

        await Service(db).FindOrCreateAsync(ZoneId, theirs.Id);

        var resumed = await Service(db, resumable: Room()).FindResumableAsync(ZoneId, VaultSlug, mine.Id);

        resumed.Should().BeNull("a character never resumes into a run that is not theirs");
    }

    [Fact]
    public async Task FindResumable_IgnoresARunThatIsNoLongerOpen()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var solo = await SeedCharacterAsync(db, await SeedAccountAsync(db), "Lonewalker");

        var service = Service(db);
        var run = await service.FindOrCreateAsync(ZoneId, solo.Id);
        run.State = ZoneInstanceState.Empty;
        await db.SaveChangesAsync();

        var resumed = await Service(db, resumable: Room()).FindResumableAsync(ZoneId, VaultSlug, solo.Id);

        resumed.Should().BeNull("an emptied run is waiting to be purged, not to be resumed");
    }

    [Fact]
    public async Task FindResumable_IgnoresASharedLocation()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var solo = await SeedCharacterAsync(db, await SeedAccountAsync(db), "Lonewalker");

        await Service(db).FindOrCreateAsync(ZoneId, solo.Id);

        var resumed = await Service(db, resumable: Room(isInstanced: false))
            .FindResumableAsync(ZoneId, VaultSlug, solo.Id);

        resumed.Should().BeNull("a shared location has no run to be inside");
    }

    [Fact]
    public async Task FindResumable_IgnoresALocationFromAnotherZone()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var solo = await SeedCharacterAsync(db, await SeedAccountAsync(db), "Lonewalker");

        await Service(db).FindOrCreateAsync(ZoneId, solo.Id);

        var resumed = await Service(db, resumable: Room(zoneId: "drowning-pits"))
            .FindResumableAsync(ZoneId, VaultSlug, solo.Id);

        resumed.Should().BeNull("a slug that matches in another zone must not hand over a run");
    }

    [Fact]
    public async Task FindResumable_DoesNotOpenARun()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var solo = await SeedCharacterAsync(db, await SeedAccountAsync(db), "Lonewalker");

        var resumed = await Service(db, resumable: Room()).FindResumableAsync(ZoneId, VaultSlug, solo.Id);

        resumed.Should().BeNull();
        (await db.ZoneInstances.CountAsync()).Should().Be(0, "a resume only ever re-enters an existing attempt");
    }

    [Fact]
    public async Task FindResumable_IgnoresAMissingLocation()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var solo = await SeedCharacterAsync(db, await SeedAccountAsync(db), "Lonewalker");

        var resumed = await Service(db).FindResumableAsync(ZoneId, "no-such-room", solo.Id);

        resumed.Should().BeNull();
    }
}
