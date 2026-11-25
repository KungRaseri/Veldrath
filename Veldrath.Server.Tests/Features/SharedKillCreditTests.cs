using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RealmEngine.Shared.Abstractions;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters;
using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Features.Quest;
using Veldrath.Server.Features.Zones;
using Veldrath.Server.Hubs;
using Veldrath.Server.Settings;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// One shared kill is credited exactly once, however many callers were fighting the enemy.
/// </summary>
/// <remarks>
/// The enemy row is shared and every read materialises its own <see cref="SpawnedEnemy"/>, so two fighters who act
/// inside the same window each hold a copy that still sees the enemy alive and each walks into the defeated branch.
/// Only the caller that wins the store's death transition may credit the kill: the winner's transition already covers
/// every participant, so a second credit advanced each of their kill objectives twice for one kill, and a second
/// loot roll handed out a second copy of the drop.
/// </remarks>
public class SharedKillCreditTests : IDisposable
{
    private const string ZoneGroup = "test/zone";
    private const string LocationSlug = "dungeon";
    private const string ArchetypeSlug = "goblin-scout";
    private const string EnemyName = "Goblin Scout";
    private const string LootTableSlug = "goblin-common-drops";
    private const string LootItemSlug = "health-potion";

    private readonly TestDbContextFactory _dbFactory = new();
    private readonly FakeEnemyInstanceStore _enemies = new();
    private readonly FakeCombatSessionStore _sessions = new();
    private readonly Mock<ILootTableRepository> _lootTables = new();

    public void Dispose() => _dbFactory.Dispose();

    // ── Handlers ───────────────────────────────────────────────────────────────

    private static IZoneInstanceService MakeInstances(ApplicationDbContext db) =>
        new EfCoreZoneInstanceService(
            db,
            new CharacterRepository(db),
            new CharacterUnlockedLocationRepository(db),
            Mock.Of<IZoneLocationRepository>(),
            NullLogger<EfCoreZoneInstanceService>.Instance);

    private UseAbilityInCombatHubCommandHandler MakeAbilityHandler(ApplicationDbContext db) =>
        new(
            new CharacterRepository(db),
            Mock.Of<ICharacterClassRepository>(),
            Mock.Of<IPowerRepository>(),
            _enemies,
            _sessions,
            MakeInstances(db),
            Options.Create(new DeathPenaltyOptions()),
            _lootTables.Object,
            NullLogger<UseAbilityInCombatHubCommandHandler>.Instance);

    private AttackEnemyHubCommandHandler MakeMeleeHandler(ApplicationDbContext db) =>
        new(
            new CharacterRepository(db),
            Mock.Of<ICharacterClassRepository>(),
            _enemies,
            _sessions,
            MakeInstances(db),
            Options.Create(new DeathPenaltyOptions()),
            _lootTables.Object,
            NullLogger<AttackEnemyHubCommandHandler>.Instance);

    // ── Arrangement helpers ───────────────────────────────────────────────────

    private static async Task<Character> SeedCharacterAsync(
        ApplicationDbContext db,
        string abilities = "[]",
        string? attrsJson = null)
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
            Attributes = attrsJson ?? """{"CurrentMana":50,"MaxMana":50,"CurrentHealth":50,"MaxHealth":100}""",
            AbilitiesBlob = abilities,
        };
        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return character;
    }

    private SpawnedEnemy AddEnemy(int hp, params Guid[] participants)
    {
        var enemy = new SpawnedEnemy
        {
            Name = EnemyName,
            ArchetypeSlug = ArchetypeSlug,
            Level = 1,
            CurrentHealth = hp,
            MaxHealth = Math.Max(1, hp),
            BaseXp = 20,
            GoldReward = 10,
            // Carried the way the EF store carries them: the room-clear check counts rows by this pair.
            ZoneGroup = ZoneGroup,
            LocationSlug = LocationSlug,
            Template = new RealmEngine.Shared.Models.Enemy
            {
                Slug = ArchetypeSlug,
                LootTableIds = [LootTableSlug],
            },
        };

        foreach (var participant in participants)
        {
            enemy.Participants.Add(participant);
            enemy.DamageContributions[participant] = 5;
        }

        _enemies.Add(ZoneGroup, LocationSlug, enemy);
        return enemy;
    }

    private void SetSession(Guid characterId, Guid enemyId) =>
        _sessions.Set(
            characterId,
            new ActiveCombatSession(ZoneGroup, LocationSlug, enemyId, false, 0, DateTimeOffset.UtcNow));

    private void ArrangeGuaranteedLoot() =>
        _lootTables
            .Setup(tables => tables.GetBySlugAsync(LootTableSlug))
            .ReturnsAsync(new RealmEngine.Shared.Models.LootTableData
            {
                Slug = LootTableSlug,
                Entries =
                [
                    new RealmEngine.Shared.Models.LootTableEntryData
                    {
                        ItemSlug = LootItemSlug,
                        DropWeight = 100,
                        QuantityMin = 1,
                        QuantityMax = 1,
                        IsGuaranteed = true,
                    },
                ],
            });

    /// <summary>
    /// A second view of the same stored enemy, the way the EF store materialises one per read: full health, carrying
    /// the participants the fight already had. Two of these are what two callers acting inside the same window hold.
    /// </summary>
    private static SpawnedEnemy StaleCopyOf(SpawnedEnemy stored)
    {
        var copy = new SpawnedEnemy
        {
            Id = stored.Id,
            ZoneGroup = stored.ZoneGroup,
            LocationSlug = stored.LocationSlug,
            ZoneInstanceId = stored.ZoneInstanceId,
            ArchetypeSlug = stored.ArchetypeSlug,
            InstanceSlug = stored.InstanceSlug,
            Name = stored.Name,
            Level = stored.Level,
            CurrentHealth = stored.MaxHealth,
            MaxHealth = stored.MaxHealth,
            BaseXp = stored.BaseXp,
            GoldReward = stored.GoldReward,
            Template = stored.Template,
        };

        foreach (var participant in stored.Participants)
        {
            copy.Participants.Add(participant);
        }

        foreach (var (characterId, damage) in stored.DamageContributions)
        {
            copy.DamageContributions[characterId] = damage;
        }

        return copy;
    }

    private static async Task<int> CarriedAsync(ApplicationDbContext db, Guid characterId, string itemSlug)
    {
        var blob = await db.Characters
            .AsNoTracking()
            .Where(character => character.Id == characterId)
            .Select(character => character.InventoryBlob)
            .SingleAsync();

        return CharacterBag.Count(CharacterBag.Read(blob), itemSlug);
    }

    // ── The losing caller ─────────────────────────────────────────────────────

    [Fact]
    public async Task An_Ability_That_Loses_The_Death_Race_Credits_Rolls_And_Clears_Nothing()
    {
        await using var db = _dbFactory.CreateContext();
        var caster = await SeedCharacterAsync(db, """["fireball"]""");
        var ally = await SeedCharacterAsync(db);
        var stored = AddEnemy(hp: 1, caster.Id, ally.Id);
        SetSession(caster.Id, stored.Id);
        ArrangeGuaranteedLoot();

        // Another turn claimed the transition first. This caller's copy was materialised while the goblin was still
        // standing, which is exactly what a concurrent read of the shared row hands back.
        (await _enemies.TryMarkDeadAsync(stored.Id, CancellationToken.None))
            .Should().NotBeNull("arranging the caller that already won the death transition");
        _enemies.PendingReads.Enqueue(StaleCopyOf(stored));

        var result = await MakeAbilityHandler(db).Handle(
            new UseAbilityInCombatHubCommand(caster.Id, "fireball"),
            CancellationToken.None);

        result.EnemyDefeated.Should().BeTrue("this caller's fight is over too — the enemy is dead");
        result.ParticipantIds.Should().BeEmpty(
            "the winner already credited every participant; a second set would advance each of their kill "
            + "objectives twice for one kill");
        result.ItemsDropped.Should().BeEmpty("the winner rolled the table");
        result.ClearedInstanceId.Should().BeNull("the winner recorded the clear");
        (await CarriedAsync(db, caster.Id, LootItemSlug)).Should()
            .Be(0, "a lost death race must not hand out a second drop");
    }

    [Fact]
    public async Task A_Melee_Blow_That_Loses_The_Death_Race_Credits_Rolls_And_Clears_Nothing()
    {
        await using var db = _dbFactory.CreateContext();
        var fighter = await SeedCharacterAsync(db);
        var ally = await SeedCharacterAsync(db);
        var stored = AddEnemy(hp: 1, fighter.Id, ally.Id);
        SetSession(fighter.Id, stored.Id);
        ArrangeGuaranteedLoot();

        (await _enemies.TryMarkDeadAsync(stored.Id, CancellationToken.None))
            .Should().NotBeNull("arranging the caller that already won the death transition");
        _enemies.PendingReads.Enqueue(StaleCopyOf(stored));

        var result = await MakeMeleeHandler(db).Handle(
            new AttackEnemyHubCommand(fighter.Id),
            CancellationToken.None);

        result.EnemyDefeated.Should().BeTrue("this caller's fight is over too — the enemy is dead");
        result.ParticipantIds.Should().BeEmpty(
            "the winner already credited every participant; a second set would advance each of their kill "
            + "objectives twice for one kill");
        result.ItemsDropped.Should().BeEmpty("the winner rolled the table");
        (await CarriedAsync(db, fighter.Id, LootItemSlug)).Should()
            .Be(0, "a lost death race must not hand out a second drop");
    }

    // ── Both callers, through the hub ────────────────────────────────────────

    [Fact]
    public async Task A_Shared_Kill_Seen_Dead_By_Two_Callers_Advances_Each_Participants_Kill_Objective_Once()
    {
        await using var db = _dbFactory.CreateContext();

        var casterAccount = await HubHarness.SeedAccountAsync(db);
        var caster = await HubHarness.SeedCharacterAsync(
            db, casterAccount.Id, attributes: """{"CurrentMana":50,"MaxMana":50}""");
        caster.AbilitiesBlob = """["fireball"]""";

        var fighterAccount = await HubHarness.SeedAccountAsync(db);
        var fighter = await HubHarness.SeedCharacterAsync(db, fighterAccount.Id);
        await db.SaveChangesAsync();

        var enemy = AddEnemy(hp: 1, caster.Id, fighter.Id);
        SetSession(caster.Id, enemy.Id);
        SetSession(fighter.Id, enemy.Id);
        ArrangeGuaranteedLoot();

        var ability = MakeAbilityHandler(db);
        var melee = MakeMeleeHandler(db);

        // The real handlers behind the hub, so the advances counted here are what two genuine turns produce rather
        // than what two hand-written results would.
        var mediator = new Mock<ISender>();
        mediator
            .Setup(sender => sender.Send(
                It.IsAny<IRequest<UseAbilityInCombatHubResult>>(), It.IsAny<CancellationToken>()))
            .Returns((IRequest<UseAbilityInCombatHubResult> request, CancellationToken ct) =>
                ability.Handle((UseAbilityInCombatHubCommand)request, ct));
        mediator
            .Setup(sender => sender.Send(
                It.IsAny<IRequest<AttackEnemyHubResult>>(), It.IsAny<CancellationToken>()))
            .Returns((IRequest<AttackEnemyHubResult> request, CancellationToken ct) =>
                melee.Handle((AttackEnemyHubCommand)request, ct));
        mediator
            .Setup(sender => sender.Send(
                It.IsAny<IRequest<AdvanceQuestObjectivesHubResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdvanceQuestObjectivesHubResult { Success = true, QuestsChanged = 1 });

        var (hub, _, _, ctx, _, _) = HubHarness.Create(
            db, casterAccount.Id, mediator, sessions: _sessions, enemies: _enemies);
        ctx.EnterZoneState(caster.Id);

        // The ability lands the killing blow and wins the transition.
        await hub.UseAbilityInCombat(new UseAbilityInCombatHubRequest("fireball"));

        // The second fighter's turn was already in flight: its copy read the goblin alive, and its session was read
        // before the winner's kill released it. Both are arrangement for the same race, not production behaviour.
        _enemies.PendingReads.Enqueue(StaleCopyOf(enemy));
        SetSession(fighter.Id, enemy.Id);
        ctx.Items["CharacterId"] = fighter.Id;

        await hub.AttackEnemy();

        // Slug plus legacy display name, once per participant. Two full sets — the winner's and the loser's — would
        // be eight advances for one kill, which is the defect this test pins.
        var advances = mediator.Invocations
            .Select(invocation => invocation.Arguments[0])
            .OfType<AdvanceQuestObjectivesHubCommand>()
            .Select(command => (command.CharacterId, command.Key))
            .ToList();

        advances.Should().BeEquivalentTo([
            (caster.Id, ArchetypeSlug),
            (caster.Id, EnemyName),
            (fighter.Id, ArchetypeSlug),
            (fighter.Id, EnemyName),
        ]);

        // And the kill paid out once: one drop, in the winner's bag.
        (await CarriedAsync(db, caster.Id, LootItemSlug)).Should().Be(1);
        (await CarriedAsync(db, fighter.Id, LootItemSlug)).Should()
            .Be(0, "the loser's turn rolled nothing, so it cannot have received a second drop");
    }
}
