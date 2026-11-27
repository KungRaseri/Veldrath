using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RealmEngine.Shared.Abstractions;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Features.Zones;
using Veldrath.Server.Settings;
using Veldrath.Server.Tests.Infrastructure;
using MapGraph = RealmEngine.Shared.Models.MapGraph;
using MapNodeEntry = RealmEngine.Shared.Models.MapNodeEntry;
using MapNodeType = RealmEngine.Shared.Models.MapNodeType;
using MapType = RealmEngine.Shared.Models.MapType;
using ZoneLocationEntry = RealmEngine.Shared.Models.ZoneLocationEntry;

namespace Veldrath.Server.Tests.Features;

/// <summary>Unit tests for <see cref="RespawnHubCommandHandler"/>.</summary>
public class RespawnHubCommandHandlerTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory = new();
    private readonly FakeCombatSessionStore _sessions = new();

    private RespawnHubCommandHandler MakeHandler(
        ApplicationDbContext db,
        IZoneLocationRepository? zoneLocations = null,
        DeathPenaltyOptions? options = null,
        IMapGraphRepository? maps = null,
        IZoneInstanceService? zoneInstances = null) =>
        new(
            new CharacterRepository(db),
            _sessions,
            new ZoneLobbyResolver(
                zoneLocations ?? ZoneLocationsWith(),
                new CharacterUnlockedLocationRepository(db)),
            maps ?? MapsWith(null).Object,
            zoneInstances ?? ZoneInstances(db, zoneLocations ?? ZoneLocationsWith()),
            Options.Create(options ?? new DeathPenaltyOptions()),
            NullLogger<RespawnHubCommandHandler>.Instance);

    /// <summary>Builds the real run service over the test's context and catalog, the way the server resolves it.</summary>
    /// <param name="db">Context the run rows live in.</param>
    /// <param name="locations">Zone-location catalog the service reads a room from.</param>
    /// <returns>The service the handler reads runs with.</returns>
    private static IZoneInstanceService ZoneInstances(
        ApplicationDbContext db,
        IZoneLocationRepository locations) =>
        new EfCoreZoneInstanceService(
            db,
            new CharacterRepository(db),
            new CharacterUnlockedLocationRepository(db),
            locations,
            NullLogger<EfCoreZoneInstanceService>.Instance);

    public void Dispose() => _dbFactory.Dispose();

    // ── A2: safe respawn location ──────────────────────────────────────────────

    private static IZoneLocationRepository ZoneLocationsWith(params ZoneLocationEntry[] locations) =>
        new FakeZoneLocationRepository(locations);

    private static ZoneLocationEntry Location(
        string slug,
        string displayName,
        bool safeRespawn = false,
        bool hidden = false,
        bool instanced = false) =>
        new(slug, displayName, instanced ? "dungeons" : "locations", "zone-1", 10, null, null,
            IsHidden: hidden, IsSafeRespawn: safeRespawn, IsInstanced: instanced);

    private static Mock<IMapGraphRepository> MapsWith(MapGraph? zoneMap)
    {
        var mock = new Mock<IMapGraphRepository>();
        mock.Setup(repo => repo.GetMapForZoneAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(zoneMap);
        return mock;
    }

    /// <summary>Builds a zone map whose entry node is followed by one location node per authored location.</summary>
    private static MapGraph ZoneMap(string zoneId, params (string Slug, string Name)[] locations) =>
        new(
            $"zone:{zoneId}",
            MapType.Zone,
            zoneId,
            "Zone",
            string.Empty,
            $"zone:{zoneId}:entry",
            [
                new MapNodeEntry(
                    $"zone:{zoneId}:entry", $"zone:{zoneId}", MapNodeType.EntryPoint, null, "Arrival", string.Empty,
                    null, null, null, true, false, null, null, false, null, null, null, 0),
                .. locations.Select((location, index) => new MapNodeEntry(
                    $"zone:{zoneId}:location:{location.Slug}", $"zone:{zoneId}", MapNodeType.Location, location.Slug,
                    location.Name, string.Empty, null, null, null, false, false, null, null, false, null, null, null,
                    (index + 1) * 10)),
            ],
            []);

    private static async Task<Character> SeedDeadCharacterAsync(ApplicationDbContext db, string zoneId = "zone-1")
    {
        var character = await SeedCharacterAsync(
            db, """{"CurrentHealth":0,"MaxHealth":100,"CurrentMana":0,"MaxMana":40}""");
        character.CurrentZoneId = zoneId;
        await db.SaveChangesAsync();
        return character;
    }

    /// <summary>Records that the character has opened a hidden location, the way discovery does in play.</summary>
    /// <param name="db">Context to write to.</param>
    /// <param name="characterId">Character receiving the unlock.</param>
    /// <param name="locationSlug">Hidden location they may now see.</param>
    private static async Task UnlockAsync(ApplicationDbContext db, Guid characterId, string locationSlug)
    {
        db.CharacterUnlockedLocations.Add(new CharacterUnlockedLocation
        {
            CharacterId = characterId,
            LocationSlug = locationSlug,
            UnlockSource = "test",
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_Moves_The_Character_To_The_Zones_Safe_Location()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedDeadCharacterAsync(db);
        var zoneLocations = ZoneLocationsWith(Location("market", "The Market"), Location("inn", "The Grey Cup", safeRespawn: true));

        var result = await MakeHandler(db, zoneLocations)
            .Handle(new RespawnHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.LocationSlug.Should().Be("inn");
        result.LocationDisplayName.Should().Be("The Grey Cup");
        result.UsedFallbackRespawn.Should().BeFalse("the zone authored a safe location");
        result.Warning.Should().BeNull();

        var storedSlug = await db.Characters.AsNoTracking()
            .Where(c => c.Id == character.Id).Select(c => c.CurrentZoneLocationSlug).SingleAsync();
        storedSlug.Should().Be("inn", "the player must actually move, not just be told about it");
    }

    [Fact]
    public async Task Handle_Moves_The_Map_Node_To_The_Safe_Locations_Node()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedDeadCharacterAsync(db);
        character.CurrentMapId = "zone:zone-1";
        character.CurrentNodeId = "zone:zone-1:location:market"; // where the character fell
        await db.SaveChangesAsync();

        var zoneLocations = ZoneLocationsWith(
            Location("market", "The Market"),
            Location("inn", "The Grey Cup", safeRespawn: true));
        var maps = MapsWith(ZoneMap("zone-1", ("market", "The Market"), ("inn", "The Grey Cup")));

        var result = await MakeHandler(db, zoneLocations, maps: maps.Object)
            .Handle(new RespawnHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue();

        var stored = await db.Characters.AsNoTracking().SingleAsync(c => c.Id == character.Id);
        stored.CurrentMapId.Should().Be("zone:zone-1");
        stored.CurrentNodeId.Should().Be(
            "zone:zone-1:location:inn",
            "a resumed client wakes from the node, so moving only the slug sends it back to the zone's arrival node");
        stored.CurrentZoneLocationSlug.Should().Be("inn");
    }

    [Fact]
    public async Task Handle_Falls_Back_To_The_First_Location_When_No_Safe_One_Is_Authored()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedDeadCharacterAsync(db);
        var zoneLocations = ZoneLocationsWith(Location("market", "The Market"));

        var result = await MakeHandler(db, zoneLocations)
            .Handle(new RespawnHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue("a degraded respawn still has to leave the player playable");
        result.LocationSlug.Should().Be("market");
        result.UsedFallbackRespawn.Should().BeTrue();
        result.Warning.Should().Contain("no safe respawn point");
    }

    [Fact]
    public async Task Handle_Moves_To_A_Hidden_Safe_Location_The_Character_Unlocked()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedDeadCharacterAsync(db);
        await UnlockAsync(db, character.Id, "strangled-oak");
        var zoneLocations = ZoneLocationsWith(
            Location("market", "The Market"),
            Location("strangled-oak", "The Strangled Oak", safeRespawn: true, hidden: true));

        var result = await MakeHandler(db, zoneLocations)
            .Handle(new RespawnHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.LocationSlug.Should().Be(
            "strangled-oak", "the zone's safe location is hidden, and this character has opened it");
        result.LocationDisplayName.Should().Be("The Strangled Oak");
        result.UsedFallbackRespawn.Should().BeFalse("the lobby is real; it was merely invisible to the plain lookup");
        result.Warning.Should().BeNull();

        var storedSlug = await db.Characters.AsNoTracking()
            .Where(c => c.Id == character.Id).Select(c => c.CurrentZoneLocationSlug).SingleAsync();
        storedSlug.Should().Be("strangled-oak");
    }

    [Fact]
    public async Task Handle_Falls_Back_When_The_Safe_Location_Is_Hidden_And_Not_Unlocked()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedDeadCharacterAsync(db);
        // No unlock row: the lobby is authored but closed to this character.
        var zoneLocations = ZoneLocationsWith(
            Location("market", "The Market"),
            Location("strangled-oak", "The Strangled Oak", safeRespawn: true, hidden: true));

        var result = await MakeHandler(db, zoneLocations)
            .Handle(new RespawnHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue("a degraded respawn still has to leave the player playable");
        result.LocationSlug.Should().Be("market", "the locked lobby is not somewhere this character may wake");
        result.UsedFallbackRespawn.Should().BeTrue();
        result.Warning.Should().Contain(
            "not yet open to you", "the zone has a safe haven — saying it has none would blame the content");
    }

    [Fact]
    public async Task Handle_Leaves_The_Character_In_Place_When_The_Zone_Has_No_Locations()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedDeadCharacterAsync(db);
        var zoneLocations = ZoneLocationsWith();

        var result = await MakeHandler(db, zoneLocations)
            .Handle(new RespawnHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.LocationSlug.Should().BeNull();
        result.UsedFallbackRespawn.Should().BeTrue();
        result.Warning.Should().Contain("respawn where you fell");

        var storedSlug = await db.Characters.AsNoTracking()
            .Where(c => c.Id == character.Id).Select(c => c.CurrentZoneLocationSlug).SingleAsync();
        storedSlug.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Applies_Death_Sickness_For_The_Configured_Window()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedDeadCharacterAsync(db);
        var options = new DeathPenaltyOptions { DeathSicknessMinutes = 5, DeathSicknessDamagePenaltyPercent = 25 };

        var result = await MakeHandler(db, ZoneLocationsWith(Location("inn", "The Grey Cup", safeRespawn: true)), options)
            .Handle(new RespawnHubCommand(character.Id), CancellationToken.None);

        result.SicknessExpiresAt.Should().NotBeNull();
        result.SicknessExpiresAt!.Value.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(5), TimeSpan.FromMinutes(1));

        var stored = await db.Characters.AsNoTracking().SingleAsync(c => c.Id == character.Id);
        stored.Attributes.Should().Contain(DeathPenalties.SicknessUntilKey);
        stored.Attributes.Should().Contain(DeathPenalties.SicknessPenaltyKey);
    }

    [Fact]
    public async Task Handle_Clears_Any_Stale_Sickness_When_The_Window_Is_Disabled()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db,
            $$"""{"CurrentHealth":0,"MaxHealth":100,"{{DeathPenalties.SicknessUntilKey}}":99999999,"{{DeathPenalties.SicknessPenaltyKey}}":25}""");
        character.CurrentZoneId = "zone-1";
        await db.SaveChangesAsync();
        var options = new DeathPenaltyOptions { DeathSicknessMinutes = 0 };

        var result = await MakeHandler(db, ZoneLocationsWith(Location("inn", "Inn", safeRespawn: true)), options)
            .Handle(new RespawnHubCommand(character.Id), CancellationToken.None);

        result.SicknessExpiresAt.Should().BeNull();
        var stored = await db.Characters.AsNoTracking().SingleAsync(c => c.Id == character.Id);
        stored.Attributes.Should().NotContain(DeathPenalties.SicknessUntilKey, "a disabled sickness must not linger");
    }

    // ── Runs: the run a death happened inside ─────────────────────────────────

    /// <summary>
    /// Seeds a dead character standing in an instanced room with a run behind it, which is the state every
    /// run-scoped respawn starts from.
    /// </summary>
    /// <param name="db">Context to write to.</param>
    /// <param name="state">State of the run the character is inside.</param>
    /// <param name="runZoneId">Zone the run belongs to, so a test can point it at another zone.</param>
    /// <param name="roomSlug">Instanced room the character fell in.</param>
    /// <returns>The character and the run.</returns>
    private static async Task<(Character Character, ZoneInstance Run)> SeedDeadInRunAsync(
        ApplicationDbContext db,
        ZoneInstanceState state,
        string runZoneId = "zone-1",
        string roomSlug = "crypt")
    {
        await EnsureZoneAsync(db, "zone-1");
        await EnsureZoneAsync(db, runZoneId);

        var character = await SeedDeadCharacterAsync(db);
        character.CurrentZoneLocationSlug = roomSlug;
        character.CurrentMapId = "zone:zone-1";
        character.CurrentNodeId = $"zone:zone-1:location:{roomSlug}";
        await db.SaveChangesAsync();

        var run = new ZoneInstance
        {
            ZoneId = runZoneId,
            CreatedByCharacterId = character.Id,
            ZoneGroup = $"zone:{runZoneId}#i1:{Guid.NewGuid():D}",
            State = state,
        };
        db.ZoneInstances.Add(run);
        await db.SaveChangesAsync();

        return (character, run);
    }

    /// <summary>
    /// Seeds a zone row so a run can point at it: <see cref="ZoneInstance.ZoneId"/> is a real foreign key, unlike the
    /// catalog's loose zone references.
    /// </summary>
    /// <param name="db">Context to write to.</param>
    /// <param name="zoneId">Zone id to create when it is not already seeded.</param>
    private static async Task EnsureZoneAsync(ApplicationDbContext db, string zoneId)
    {
        if (await db.Zones.AnyAsync(zone => zone.Id == zoneId))
            return;

        db.Zones.Add(new Zone
        {
            Id = zoneId,
            Name = zoneId,
            RegionId = "varenmark",
            Type = ZoneType.Dungeon,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>The map a run respawn is decided on: the shared anchor, the instanced room, and the run's lobby.</summary>
    /// <returns>The graph the handler reads.</returns>
    private static MapGraph RunMap() => ZoneMap(
        "zone-1", ("market", "The Market"), ("crypt", "The Crypt"), ("vault-of-echoes", "The Vault of Echoes"));

    [Fact]
    public async Task Handle_Keeps_A_Still_Open_Run_And_Wakes_At_Its_Instanced_Lobby()
    {
        await using var db = _dbFactory.CreateContext();
        var (character, run) = await SeedDeadInRunAsync(db, ZoneInstanceState.Open);

        // Both flags present: the shared anchor is safe but not instanced, the run's lobby is both.
        var zoneLocations = ZoneLocationsWith(
            Location("market", "The Market", safeRespawn: true),
            Location("crypt", "The Crypt", instanced: true),
            Location("vault-of-echoes", "The Vault of Echoes", safeRespawn: true, instanced: true));

        var result = await MakeHandler(db, zoneLocations, maps: MapsWith(RunMap()).Object)
            .Handle(new RespawnHubCommand(character.Id, run.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.LocationSlug.Should().Be(
            "vault-of-echoes", "the character kept the run, so the wake belongs inside it, not at the shared anchor");
        result.LocationDisplayName.Should().Be("The Vault of Echoes");
        result.UsedFallbackRespawn.Should().BeFalse("the run authored a lobby");
        result.RetainedZoneInstanceId.Should().Be(run.Id, "the hub leaves the group alone when the run is retained");

        var stored = await db.Characters.AsNoTracking().SingleAsync(row => row.Id == character.Id);
        stored.CurrentMapId.Should().Be("zone:zone-1");
        stored.CurrentNodeId.Should().Be("zone:zone-1:location:vault-of-echoes");
        stored.CurrentZoneLocationSlug.Should().Be("vault-of-echoes");
    }

    [Fact]
    public async Task Handle_Wakes_At_The_Runs_Lobby_When_The_Character_Has_Unlocked_It()
    {
        await using var db = _dbFactory.CreateContext();
        var (character, run) = await SeedDeadInRunAsync(db, ZoneInstanceState.Open);
        await UnlockAsync(db, character.Id, "vault-of-echoes");

        var zoneLocations = ZoneLocationsWith(
            Location("market", "The Market", safeRespawn: true),
            Location("crypt", "The Crypt", instanced: true),
            Location("vault-of-echoes", "The Vault of Echoes", safeRespawn: true, hidden: true, instanced: true));

        var result = await MakeHandler(db, zoneLocations, maps: MapsWith(RunMap()).Object)
            .Handle(new RespawnHubCommand(character.Id, run.Id), CancellationToken.None);

        result.LocationSlug.Should().Be(
            "vault-of-echoes", "an unlock makes the run's hidden lobby a destination, exactly as it does in a zone");
        result.UsedFallbackRespawn.Should().BeFalse();
        result.RetainedZoneInstanceId.Should().Be(run.Id);
    }

    [Fact]
    public async Task Handle_Stays_Where_They_Fell_When_The_Runs_Lobby_Is_Locked()
    {
        await using var db = _dbFactory.CreateContext();
        var (character, run) = await SeedDeadInRunAsync(db, ZoneInstanceState.Open);
        // No unlock row: the run authored a lobby and this character cannot use it.

        var zoneLocations = ZoneLocationsWith(
            Location("market", "The Market", safeRespawn: true),
            Location("crypt", "The Crypt", instanced: true),
            Location("vault-of-echoes", "The Vault of Echoes", safeRespawn: true, hidden: true, instanced: true));

        var result = await MakeHandler(db, zoneLocations, maps: MapsWith(RunMap()).Object)
            .Handle(new RespawnHubCommand(character.Id, run.Id), CancellationToken.None);

        result.Success.Should().BeTrue("the run is still theirs; only the lobby is closed to them");
        result.LocationSlug.Should().BeNull("a locked run lobby must never send them to the shared zone's anchor");
        result.UsedFallbackRespawn.Should().BeTrue();
        result.Warning.Should().Be(
            "This run has no safe lobby you can use; you respawn where you fell.",
            "the degradation is said once, and it explains why they did not move");
        result.RetainedZoneInstanceId.Should().Be(run.Id);

        var stored = await db.Characters.AsNoTracking().SingleAsync(row => row.Id == character.Id);
        stored.CurrentNodeId.Should().Be("zone:zone-1:location:crypt", "staying put keeps the saved position");
        stored.CurrentZoneLocationSlug.Should().Be("crypt");
    }

    [Fact]
    public async Task Handle_Stays_Where_They_Fell_When_The_Runs_Lobby_Has_No_Map_Node()
    {
        await using var db = _dbFactory.CreateContext();
        var (character, run) = await SeedDeadInRunAsync(db, ZoneInstanceState.Open);

        // The lobby is authored but the zone map never places it: a character placed there would resume nowhere.
        var zoneLocations = ZoneLocationsWith(
            Location("market", "The Market", safeRespawn: true),
            Location("crypt", "The Crypt", instanced: true),
            Location("vault-of-echoes", "The Vault of Echoes", safeRespawn: true, instanced: true));
        var mapWithoutLobby = ZoneMap("zone-1", ("market", "The Market"), ("crypt", "The Crypt"));

        var result = await MakeHandler(db, zoneLocations, maps: MapsWith(mapWithoutLobby).Object)
            .Handle(new RespawnHubCommand(character.Id, run.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.LocationSlug.Should().BeNull("an unplaced lobby is not a destination");
        result.UsedFallbackRespawn.Should().BeTrue();
        result.Warning.Should().Contain("no safe lobby you can use");
        result.RetainedZoneInstanceId.Should().Be(run.Id);

        var stored = await db.Characters.AsNoTracking().SingleAsync(row => row.Id == character.Id);
        stored.CurrentNodeId.Should().Be("zone:zone-1:location:crypt");
    }

    [Fact]
    public async Task Handle_Stays_Where_They_Fell_When_The_Run_Authors_No_Lobby()
    {
        await using var db = _dbFactory.CreateContext();
        var (character, run) = await SeedDeadInRunAsync(db, ZoneInstanceState.Open);

        var zoneLocations = ZoneLocationsWith(
            Location("market", "The Market", safeRespawn: true),
            Location("crypt", "The Crypt", instanced: true));

        var result = await MakeHandler(db, zoneLocations, maps: MapsWith(RunMap()).Object)
            .Handle(new RespawnHubCommand(character.Id, run.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.LocationSlug.Should().BeNull("a run with no lobby heals in place rather than in the shared zone");
        result.RetainedZoneInstanceId.Should().Be(run.Id);
        result.Warning.Should().Contain("no safe lobby you can use");
    }

    [Fact]
    public async Task Handle_Refuses_The_Respawn_When_The_Run_Offers_No_Destination()
    {
        await using var db = _dbFactory.CreateContext();
        var (character, run) = await SeedDeadInRunAsync(db, ZoneInstanceState.Open);

        // The only instanced room is the one they fell in, and their saved node no longer names it: there is no
        // lobby to move to and no coherent position to keep.
        character.CurrentNodeId = "zone:zone-1:entry";
        await db.SaveChangesAsync();
        _sessions.Set(
            character.Id,
            new ActiveCombatSession("zone", "crypt", Guid.NewGuid(), false, 0, DateTimeOffset.UtcNow));

        var zoneLocations = ZoneLocationsWith(
            Location("market", "The Market", safeRespawn: true),
            Location("crypt", "The Crypt", instanced: true));

        var result = await MakeHandler(db, zoneLocations, maps: MapsWith(RunMap()).Object)
            .Handle(new RespawnHubCommand(character.Id, run.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("No valid respawn location is available.");

        // Nothing durable was written: no heal, no move, no combat-session cleanup.
        var stored = await db.Characters.AsNoTracking().SingleAsync(row => row.Id == character.Id);
        stored.Attributes.Should().Contain("\"CurrentHealth\":0", "a refused respawn must not heal the character");
        stored.CurrentNodeId.Should().Be("zone:zone-1:entry");
        stored.CurrentZoneLocationSlug.Should().Be("crypt");
        _sessions.IsInCombat(character.Id).Should().BeTrue("the refused respawn left the death state untouched");
    }

    [Theory]
    [InlineData(ZoneInstanceState.Empty, "zone-1")]
    [InlineData(ZoneInstanceState.Purging, "zone-1")]
    [InlineData(ZoneInstanceState.Open, "zone-2")]
    public async Task Handle_Wakes_At_The_Mapped_Shared_Anchor_When_The_Run_Is_Unavailable(
        ZoneInstanceState state, string runZoneId)
    {
        await using var db = _dbFactory.CreateContext();
        var (character, run) = await SeedDeadInRunAsync(db, state, runZoneId);

        var zoneLocations = ZoneLocationsWith(
            Location("market", "The Market", safeRespawn: true),
            Location("crypt", "The Crypt", instanced: true),
            Location("vault-of-echoes", "The Vault of Echoes", safeRespawn: true, instanced: true));

        var result = await MakeHandler(db, zoneLocations, maps: MapsWith(RunMap()).Object)
            .Handle(new RespawnHubCommand(character.Id, run.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.LocationSlug.Should().Be(
            "market", "an unavailable run cannot take them, so only a shared anchor will do");
        result.RetainedZoneInstanceId.Should().BeNull("the hub has to move the connection to the shared scope");

        var stored = await db.Characters.AsNoTracking().SingleAsync(row => row.Id == character.Id);
        stored.CurrentNodeId.Should().Be("zone:zone-1:location:market");
        stored.CurrentZoneLocationSlug.Should().Be("market");
    }

    [Fact]
    public async Task Handle_Refuses_The_Respawn_When_A_Closed_Run_Has_No_Mapped_Shared_Anchor()
    {
        await using var db = _dbFactory.CreateContext();
        var (character, run) = await SeedDeadInRunAsync(db, ZoneInstanceState.Empty);

        // A shared safe location exists in the catalog, but the zone map never places it, so there is nowhere a
        // client could resume.
        var zoneLocations = ZoneLocationsWith(Location("market", "The Market", safeRespawn: true));
        var mapWithoutMarket = ZoneMap("zone-1", ("crypt", "The Crypt"));

        var result = await MakeHandler(db, zoneLocations, maps: MapsWith(mapWithoutMarket).Object)
            .Handle(new RespawnHubCommand(character.Id, run.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("No valid respawn location is available.");

        var stored = await db.Characters.AsNoTracking().SingleAsync(row => row.Id == character.Id);
        stored.Attributes.Should().Contain("\"CurrentHealth\":0");
        stored.CurrentNodeId.Should().Be("zone:zone-1:location:crypt");
    }

    [Fact]
    public async Task Handle_Does_Not_Read_A_Run_For_An_Ordinary_Respawn()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedDeadCharacterAsync(db);

        // Strict: any call at all fails the test, which is the point — a death outside a run has no run to read.
        var runs = new Mock<IZoneInstanceService>(MockBehavior.Strict);

        var result = await MakeHandler(db, ZoneLocationsWith(Location("market", "The Market")), zoneInstances: runs.Object)
            .Handle(new RespawnHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RetainedZoneInstanceId.Should().BeNull("there was no run to keep");
        runs.VerifyNoOtherCalls();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static async Task<Character> SeedCharacterAsync(
        ApplicationDbContext db,
        string? attrsJson = null,
        DateTimeOffset? deletedAt = null)
    {
        var account = new PlayerAccount { UserName = $"u_{Guid.NewGuid():N}" };
        account.NormalizedUserName = account.UserName.ToUpperInvariant();
        db.Users.Add(account);
        var character = new Character
        {
            AccountId = account.Id,
            Name = $"Char_{Guid.NewGuid():N}",
            ClassName = "Warrior",
            SlotIndex = 1,
            Attributes = attrsJson ?? "{}",
            DeletedAt = deletedAt,
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return character;
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Returns_Error_When_Character_Not_Found()
    {
        await using var db = _dbFactory.CreateContext();

        var result = await MakeHandler(db).Handle(new RespawnHubCommand(Guid.NewGuid()), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_Returns_Not_Found_For_Hardcore_Character_After_Death()
    {
        // After a HC character is killed, their record is soft-deleted.
        // GetByIdAsync filters deleted records, so respawn receives "not found".
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db, deletedAt: DateTimeOffset.UtcNow);

        var result = await MakeHandler(db).Handle(new RespawnHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_Returns_Error_When_Character_Is_Alive()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db, attrsJson: """{"CurrentHealth":50,"MaxHealth":100}""");

        var result = await MakeHandler(db).Handle(new RespawnHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not dead");
    }

    [Fact]
    public async Task Handle_Respawns_Dead_Character_With_Partial_Health()
    {
        await using var db = _dbFactory.CreateContext();
        // No Attributes → CurrentHealth defaults to 0 (dead), MaxHealth = Level * 10 = 10.
        var character = await SeedCharacterAsync(db);

        var result = await MakeHandler(db).Handle(new RespawnHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.CurrentHealth.Should().BeGreaterThanOrEqualTo(1);
        // 25% of 10 = 2 (max(1, 10/4) = 2)
        result.CurrentHealth.Should().BeLessThanOrEqualTo(10);
    }

    [Fact]
    public async Task Handle_Respawn_Removes_Combat_Session()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedCharacterAsync(db);
        // Fake an active combat session for this character.
        _sessions.Set(
            character.Id,
            new ActiveCombatSession("zone", "loc", Guid.NewGuid(), false, 0, DateTimeOffset.UtcNow));

        await MakeHandler(db).Handle(new RespawnHubCommand(character.Id), CancellationToken.None);

        _sessions.IsInCombat(character.Id).Should().BeFalse();
    }
}
