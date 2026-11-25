using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RealmEngine.Shared.Abstractions;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Features.Zones;
using Veldrath.Server.Hubs;
using Veldrath.Server.Settings;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>Unit tests for <see cref="UseAbilityInCombatHubCommandHandler"/>.</summary>
public class UseAbilityInCombatHubCommandHandlerTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory = new();
    private readonly FakeEnemyInstanceStore _enemies = new();
    private readonly FakeCombatSessionStore _sessions = new();
    private readonly Mock<ILootTableRepository> _lootTables = new();

    private UseAbilityInCombatHubCommandHandler MakeHandler(ApplicationDbContext db, IZoneInstanceService? instances = null) =>
        new(
            new CharacterRepository(db),
            Mock.Of<ICharacterClassRepository>(),
            Mock.Of<IPowerRepository>(),
            _enemies,
            _sessions,
            instances ?? MakeInstanceService(db),
            Options.Create(new DeathPenaltyOptions()),
            _lootTables.Object,
            NullLogger<UseAbilityInCombatHubCommandHandler>.Instance);

    /// <summary>
    /// The real run service over the test's own SQLite context, so a clear is asserted against the rows it wrote
    /// rather than against a mock that merely recorded the call.
    /// </summary>
    private static EfCoreZoneInstanceService MakeInstanceService(
        ApplicationDbContext db,
        IReadOnlyList<RealmEngine.Shared.Models.ZoneLocationEntry>? hidden = null)
    {
        var locations = new Mock<IZoneLocationRepository>();
        locations
            .Setup(repo => repo.GetHiddenByZoneIdAsync(It.IsAny<string>()))
            .ReturnsAsync((hidden ?? []).ToList());

        return new(
            db,
            new CharacterRepository(db),
            new CharacterUnlockedLocationRepository(db),
            locations.Object,
            NullLogger<EfCoreZoneInstanceService>.Instance);
    }

    private static RealmEngine.Shared.Models.ZoneLocationEntry HiddenRoom(string slug, string unlockKey) =>
        new(slug, slug, "dungeons", "the-halrow", 30, 11, 14, IsHidden: true, UnlockType: "quest", UnlockKey: unlockKey);

    /// <summary>The melee handler over the same store, so a parity test can compare the two death paths.</summary>
    private AttackEnemyHubCommandHandler MakeMeleeHandler(ApplicationDbContext db) =>
        new(
            new CharacterRepository(db),
            Mock.Of<ICharacterClassRepository>(),
            _enemies,
            _sessions,
            MakeInstanceService(db),
            Options.Create(new DeathPenaltyOptions()),
            _lootTables.Object,
            NullLogger<AttackEnemyHubCommandHandler>.Instance);

    public void Dispose() => _dbFactory.Dispose();

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static async Task<Character> SeedCharacterAsync(ApplicationDbContext db, string? attrsJson = null)
    {
        var account = new PlayerAccount { UserName = $"u_{Guid.NewGuid():N}" };
        account.NormalizedUserName = account.UserName.ToUpperInvariant();
        db.Users.Add(account);
        var character = new Character
        {
            AccountId = account.Id,
            Name = $"Char_{Guid.NewGuid():N}",
            ClassName = "Mage",
            SlotIndex = 1,
            Attributes = attrsJson ?? "{}",
            AbilitiesBlob = "[\"fireball\",\"heal\"]",
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return character;
    }

    private (string ZoneGroup, string LocationSlug, SpawnedEnemy Enemy) AddEnemy(
        int hp = 100,
        RealmEngine.Shared.Models.Enemy? template = null,
        Guid? zoneInstanceId = null,
        int baseXp = 0,
        int goldReward = 0)
    {
        var zoneGroup = $"test/{Guid.NewGuid():N}";
        const string loc = "dungeon";
        var enemy = new SpawnedEnemy
        {
            Name = "Golem",
            ArchetypeSlug = "golem",
            Level = 1,
            CurrentHealth = hp,
            MaxHealth = Math.Max(1, hp),
            BaseXp = baseXp,
            GoldReward = goldReward,
            // Carried the way the EF store carries them: the room-clear check counts rows by this pair, so a fake
            // row without it would look like it stood nowhere and could never clear anything.
            ZoneGroup = zoneGroup,
            LocationSlug = loc,
            ZoneInstanceId = zoneInstanceId,
            Template = template ?? new RealmEngine.Shared.Models.Enemy(),
        };
        _enemies.Add(zoneGroup, loc, enemy);
        return (zoneGroup, loc, enemy);
    }

    private void SetSession(Guid charId, string zoneGroup, string loc, Guid enemyId)
    {
        _sessions.Set(charId, new ActiveCombatSession(zoneGroup, loc, enemyId, false, 0, DateTimeOffset.UtcNow));
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Returns_Error_When_AbilityId_Is_Empty()
    {
        await using var db = _dbFactory.CreateContext();

        var result = await MakeHandler(db).Handle(
            new UseAbilityInCombatHubCommand(Guid.NewGuid(), ""),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty");
    }

    [Fact]
    public async Task Handle_Returns_Error_When_Not_In_Combat()
    {
        await using var db = _dbFactory.CreateContext();

        var result = await MakeHandler(db).Handle(
            new UseAbilityInCombatHubCommand(Guid.NewGuid(), "fireball"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Not in combat");
    }

    [Fact]
    public async Task Handle_Returns_Error_When_Ability_Is_On_Cooldown()
    {
        await using var db = _dbFactory.CreateContext();
        const string abilityId = "fireball";
        // Serialize attrs with cooldown already active.
        string attrs = $$"""{"CurrentMana":100,"MaxMana":100,"AbilityCooldown_{{abilityId}}":3}""";
        var character = await SeedCharacterAsync(db, attrs);
        var (zg, loc, enemy) = AddEnemy();
        SetSession(character.Id, zg, loc, enemy.Id);

        var result = await MakeHandler(db).Handle(
            new UseAbilityInCombatHubCommand(character.Id, abilityId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cooldown");
    }

    [Fact]
    public async Task Handle_Returns_Error_When_Not_Enough_Mana()
    {
        await using var db = _dbFactory.CreateContext();
        // CurrentMana=0 — cannot pay the 10-mana cost.
        var character = await SeedCharacterAsync(db, """{"CurrentMana":0,"MaxMana":100}""");
        var (zg, loc, enemy) = AddEnemy();
        SetSession(character.Id, zg, loc, enemy.Id);

        var result = await MakeHandler(db).Handle(
            new UseAbilityInCombatHubCommand(character.Id, "fireball"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("mana");
    }

    [Fact]
    public async Task Handle_Deals_Damage_When_Ability_Used_Successfully()
    {
        await using var db = _dbFactory.CreateContext();
        // Enough mana, no cooldown — ability fires.
        var character = await SeedCharacterAsync(db, """{"CurrentMana":50,"MaxMana":50}""");
        var (zg, loc, enemy) = AddEnemy(hp: 200);
        SetSession(character.Id, zg, loc, enemy.Id);

        var result = await MakeHandler(db).Handle(
            new UseAbilityInCombatHubCommand(character.Id, "fireball"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.AbilityDamage.Should().BeGreaterThan(0);
        enemy.CurrentHealth.Should().BeLessThan(200);
    }

    [Fact]
    public async Task Handle_Reports_Every_Session_The_Kill_Released()
    {
        await using var db = _dbFactory.CreateContext();
        var caster = await SeedCharacterAsync(db, """{"CurrentMana":50,"MaxMana":50}""");
        var defender = await SeedCharacterAsync(db, """{"CurrentHealth":30,"MaxHealth":100}""");
        var (zg, loc, enemy) = AddEnemy(hp: 1);
        SetSession(caster.Id, zg, loc, enemy.Id);
        SetSession(defender.Id, zg, loc, enemy.Id);

        var result = await MakeHandler(db).Handle(
            new UseAbilityInCombatHubCommand(caster.Id, "fireball"),
            CancellationToken.None);

        result.EnemyDefeated.Should().BeTrue();
        // The killing blow is shared: the defender's session is gone with the caster's, so the hub needs both ids to
        // close both panels — otherwise the defender sits on "Not in combat" for the rest of the session.
        result.ReleasedCombatCharacterIds.Should().BeEquivalentTo([caster.Id, defender.Id]);
        _sessions.IsInCombat(caster.Id).Should().BeFalse();
        _sessions.IsInCombat(defender.Id).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Reports_Every_Participant_Credited_With_The_Kill()
    {
        await using var db = _dbFactory.CreateContext();
        var caster = await SeedCharacterAsync(db, """{"CurrentMana":50,"MaxMana":50}""");
        var ally = await SeedCharacterAsync(db, """{"CurrentHealth":30,"MaxHealth":100}""");
        var bystander = await SeedCharacterAsync(db, """{"CurrentHealth":30,"MaxHealth":100}""");

        var (zg, loc, enemy) = AddEnemy(hp: 1);
        // A party member joins the fight as a participant when the leader engages, with no session of its own. This
        // one has damaged the enemy; it is the caster's ability that lands the killing blow.
        enemy.Participants.Add(ally.Id);
        enemy.DamageContributions[ally.Id] = 12;
        SetSession(caster.Id, zg, loc, enemy.Id);
        // The bystander has a fight pointed at the same enemy but never engaged it: releasing their session is a
        // panel-closing fact, not a kill credit.
        SetSession(bystander.Id, zg, loc, enemy.Id);

        var result = await MakeHandler(db).Handle(
            new UseAbilityInCombatHubCommand(caster.Id, "fireball"),
            CancellationToken.None);

        result.EnemyDefeated.Should().BeTrue();
        // The credited set is the enemy's participant set — the same set a melee kill credits — so the ally is owed
        // the kill objective even though the caster landed the blow.
        result.ParticipantIds.Should().BeEquivalentTo([caster.Id, ally.Id]);
        // The released set is wider by design and must stay its own answer: it carries a session-only defender.
        result.ReleasedCombatCharacterIds.Should().BeEquivalentTo([caster.Id, bystander.Id]);
    }

    [Fact]
    public async Task Handle_Drops_The_Same_Loot_A_Melee_Kill_Of_The_Same_Archetype_Drops()
    {
        await using var db = _dbFactory.CreateContext();
        var caster = await SeedCharacterAsync(db, """{"CurrentMana":50,"MaxMana":50}""");
        var fighter = await SeedCharacterAsync(db, """{"CurrentHealth":50,"MaxHealth":100}""");

        // One archetype, one table, two ways to die: the fireball lands on one row and the sword on the other.
        var goblinTemplate = new RealmEngine.Shared.Models.Enemy
        {
            Slug = "goblin",
            LootTableIds = ["goblin-common-drops"],
        };
        var (spellGroup, spellLocation, spellVictim) = AddEnemy(hp: 1, template: goblinTemplate);
        var (meleeGroup, meleeLocation, meleeVictim) = AddEnemy(hp: 1, template: goblinTemplate);
        SetSession(caster.Id, spellGroup, spellLocation, spellVictim.Id);
        SetSession(fighter.Id, meleeGroup, meleeLocation, meleeVictim.Id);

        _lootTables
            .Setup(tables => tables.GetBySlugAsync("goblin-common-drops"))
            .ReturnsAsync(new RealmEngine.Shared.Models.LootTableData
            {
                Slug = "goblin-common-drops",
                Entries =
                [
                    new RealmEngine.Shared.Models.LootTableEntryData
                    {
                        ItemSlug = "health-potion",
                        DropWeight = 100,
                        QuantityMin = 1,
                        QuantityMax = 1,
                        IsGuaranteed = true,
                    },
                ],
            });

        var spellResult = await MakeHandler(db).Handle(
            new UseAbilityInCombatHubCommand(caster.Id, "fireball"), CancellationToken.None);
        var meleeResult = await MakeMeleeHandler(db).Handle(
            new AttackEnemyHubCommand(fighter.Id), CancellationToken.None);

        spellResult.EnemyDefeated.Should().BeTrue();
        meleeResult.EnemyDefeated.Should().BeTrue();

        // Same roll source: both death paths call the one helper, so the same archetype drops the same thing.
        spellResult.ItemsDropped.Should().ContainSingle().Which.Should().Be(new CombatLootDrop("health-potion", 1));
        spellResult.ItemsDropped.Should().BeEquivalentTo(meleeResult.ItemsDropped);

        // A drop the player never receives is not a drop: it has to be in the caster's own bag blob.
        var casterRow = await db.Characters.AsNoTracking().SingleAsync(character => character.Id == caster.Id);
        casterRow.InventoryBlob.Should().Contain("health-potion");
    }

    [Fact]
    public async Task Handle_Records_The_RoomClear_An_Ability_Kill_Completes()
    {
        await using var db = _dbFactory.CreateContext();
        var caster = await SeedCharacterAsync(db, """{"CurrentMana":50,"MaxMana":50}""");

        // The room names a hidden chamber as its unlock, so the clear has something to report beyond the flag.
        var instances = MakeInstanceService(db, [HiddenRoom("relic-chamber", "dungeon")]);
        var run = await instances.FindOrCreateAsync("the-halrow", caster.Id);

        var (zoneGroup, location, enemy) = AddEnemy(hp: 1, zoneInstanceId: run.Id);
        SetSession(caster.Id, zoneGroup, location, enemy.Id);

        var result = await MakeHandler(db, instances).Handle(
            new UseAbilityInCombatHubCommand(caster.Id, "fireball"), CancellationToken.None);

        result.EnemyDefeated.Should().BeTrue();
        result.ClearedInstanceId.Should().Be(run.Id);
        result.ClearedLocationSlug.Should().Be(location);
        result.FirstClearOfRun.Should().BeTrue();
        result.FirstClearForCharacter.Should().BeTrue();
        result.UnlockedLocationSlugs.Should().ContainSingle().Which.Should().Be("relic-chamber");

        // The room stays quiet for the cleared-room window, the run remembers the clear, and the character
        // holds the unlock — the same three facts a melee kill leaves behind.
        _enemies.ClearedWindowApplications[$"{zoneGroup}/{location}"].Should().Be(1);
        var state = await db.ZoneInstanceLocationStates
            .SingleAsync(row => row.ZoneInstanceId == run.Id && row.LocationSlug == location);
        state.IsCleared.Should().BeTrue();
        state.FirstClearedByCharacterId.Should().Be(caster.Id);
        (await db.CharacterUnlockedLocations
                .AnyAsync(row => row.CharacterId == caster.Id && row.LocationSlug == "relic-chamber"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Reports_No_Drops_And_No_Clear_When_The_Ability_Does_Not_Kill()
    {
        await using var db = _dbFactory.CreateContext();
        var caster = await SeedCharacterAsync(db, """{"CurrentMana":50,"MaxMana":50}""");

        var instances = MakeInstanceService(db);
        var run = await instances.FindOrCreateAsync("the-halrow", caster.Id);

        // A living enemy with a guaranteed-drop table: nothing may roll while it still stands.
        var (zoneGroup, location, enemy) = AddEnemy(
            hp: 500,
            zoneInstanceId: run.Id,
            template: new RealmEngine.Shared.Models.Enemy
            {
                Slug = "goblin",
                LootTableIds = ["goblin-common-drops"],
            });
        SetSession(caster.Id, zoneGroup, location, enemy.Id);

        _lootTables
            .Setup(tables => tables.GetBySlugAsync("goblin-common-drops"))
            .ReturnsAsync(new RealmEngine.Shared.Models.LootTableData
            {
                Slug = "goblin-common-drops",
                Entries =
                [
                    new RealmEngine.Shared.Models.LootTableEntryData
                    {
                        ItemSlug = "health-potion",
                        DropWeight = 100,
                        QuantityMin = 1,
                        QuantityMax = 1,
                        IsGuaranteed = true,
                    },
                ],
            });

        var result = await MakeHandler(db, instances).Handle(
            new UseAbilityInCombatHubCommand(caster.Id, "fireball"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.EnemyDefeated.Should().BeFalse();
        result.ItemsDropped.Should().BeEmpty();
        result.ClearedInstanceId.Should().BeNull();
        result.ClearedLocationSlug.Should().BeNull();
        result.FirstClearOfRun.Should().BeFalse();
        result.FirstClearForCharacter.Should().BeFalse();
        result.UnlockedLocationSlugs.Should().BeEmpty();

        _enemies.ClearedWindowApplications.Should().BeEmpty();
        (await db.ZoneInstanceLocationStates.AnyAsync(row => row.ZoneInstanceId == run.Id)).Should().BeFalse();
        var casterRow = await db.Characters.AsNoTracking().SingleAsync(character => character.Id == caster.Id);
        (casterRow.InventoryBlob ?? string.Empty).Should().NotContain("health-potion");
    }
}
