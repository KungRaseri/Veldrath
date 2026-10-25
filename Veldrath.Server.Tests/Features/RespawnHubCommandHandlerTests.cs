using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RealmEngine.Shared.Abstractions;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Settings;
using Veldrath.Server.Tests.Infrastructure;
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
        DeathPenaltyOptions? options = null) =>
        new(
            new CharacterRepository(db),
            _sessions,
            zoneLocations ?? ZoneLocationsWith().Object,
            Options.Create(options ?? new DeathPenaltyOptions()),
            NullLogger<RespawnHubCommandHandler>.Instance);

    public void Dispose() => _dbFactory.Dispose();

    // ── A2: safe respawn location ──────────────────────────────────────────────

    private static Mock<IZoneLocationRepository> ZoneLocationsWith(params ZoneLocationEntry[] locations)
    {
        var mock = new Mock<IZoneLocationRepository>();
        mock.Setup(repo => repo.GetByZoneIdAsync(It.IsAny<string>())).ReturnsAsync([.. locations]);
        return mock;
    }

    private static ZoneLocationEntry Location(string slug, string displayName, bool safeRespawn = false) =>
        new(slug, displayName, "locations", "zone-1", 10, null, null, IsSafeRespawn: safeRespawn);

    private static async Task<Character> SeedDeadCharacterAsync(ApplicationDbContext db, string zoneId = "zone-1")
    {
        var character = await SeedCharacterAsync(
            db, """{"CurrentHealth":0,"MaxHealth":100,"CurrentMana":0,"MaxMana":40}""");
        character.CurrentZoneId = zoneId;
        await db.SaveChangesAsync();
        return character;
    }

    [Fact]
    public async Task Handle_Moves_The_Character_To_The_Zones_Safe_Location()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedDeadCharacterAsync(db);
        var zoneLocations = ZoneLocationsWith(Location("market", "The Market"), Location("inn", "The Grey Cup", safeRespawn: true));

        var result = await MakeHandler(db, zoneLocations.Object)
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
    public async Task Handle_Falls_Back_To_The_First_Location_When_No_Safe_One_Is_Authored()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedDeadCharacterAsync(db);
        var zoneLocations = ZoneLocationsWith(Location("market", "The Market"));

        var result = await MakeHandler(db, zoneLocations.Object)
            .Handle(new RespawnHubCommand(character.Id), CancellationToken.None);

        result.Success.Should().BeTrue("a degraded respawn still has to leave the player playable");
        result.LocationSlug.Should().Be("market");
        result.UsedFallbackRespawn.Should().BeTrue();
        result.Warning.Should().Contain("no safe respawn point");
    }

    [Fact]
    public async Task Handle_Leaves_The_Character_In_Place_When_The_Zone_Has_No_Locations()
    {
        await using var db = _dbFactory.CreateContext();
        var character = await SeedDeadCharacterAsync(db);
        var zoneLocations = ZoneLocationsWith();

        var result = await MakeHandler(db, zoneLocations.Object)
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

        var result = await MakeHandler(db, ZoneLocationsWith(Location("inn", "The Grey Cup", safeRespawn: true)).Object, options)
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

        var result = await MakeHandler(db, ZoneLocationsWith(Location("inn", "Inn", safeRespawn: true)).Object, options)
            .Handle(new RespawnHubCommand(character.Id), CancellationToken.None);

        result.SicknessExpiresAt.Should().BeNull();
        var stored = await db.Characters.AsNoTracking().SingleAsync(c => c.Id == character.Id);
        stored.Attributes.Should().NotContain(DeathPenalties.SicknessUntilKey, "a disabled sickness must not linger");
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
