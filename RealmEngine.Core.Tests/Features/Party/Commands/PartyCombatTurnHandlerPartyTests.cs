using System.Reflection;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Combat;
using RealmEngine.Core.Features.Combat.Commands;
using RealmEngine.Core.Features.Party.Commands;
using RealmEngine.Core.Features.Party.Services;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Tests.Infrastructure;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Party.Commands;

/// <summary>
/// Covers the party half of <see cref="PartyCombatTurnHandler"/>: ally turns (attacks, kills, heals),
/// the enemy's target choice between the player and a random ally, and the party-defeated outcome.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CombatService.ExecutePlayerAttack"/> is virtual and mocked so the player's turn is
/// scripted. <see cref="CombatService.ExecuteEnemyAttack"/> is not virtual, so the enemy turn runs the
/// real implementation; its collaborators are injected into the Moq proxy through the private fields
/// the protected test constructor leaves null (mediator, difficulty settings and the random source).
/// </para>
/// <para>
/// The handler picks the enemy's target with its own <see cref="IRandomSource"/> (60% player,
/// 40% ally); the ally-target test scripts the NextDouble and Next calls rather than repeating the
/// turn until the 40% arm happens to be taken.
/// </para>
/// </remarks>
[Trait("Category", "Feature")]
public class PartyCombatTurnHandlerPartyTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static void InjectCombatDependencies(CombatService service, IMediator mediator, IRandomSource random)
    {
        var type = typeof(CombatService);
        type.GetField("_mediator", PrivateInstance)!.SetValue(service, mediator);
        type.GetField("_combatSettings", PrivateInstance)!.SetValue(service, DifficultySettings.Normal);
        type.GetField("_random", PrivateInstance)!.SetValue(service, random);
    }

    private static Mock<IMediator> CreateStatusEffectMediator()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ProcessStatusEffectsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessStatusEffectsResult());
        return mediator;
    }

    private static Mock<CombatService> CreateMockCombatService(int playerDamage)
    {
        var mock = new Mock<CombatService>();
        mock.Setup(c => c.ExecutePlayerAttack(It.IsAny<Character>(), It.IsAny<Enemy>(), It.IsAny<bool>()))
            .ReturnsAsync(new CombatResult
            {
                Success = true,
                Damage = playerDamage,
                Message = $"Player deals {playerDamage} damage!"
            });
        InjectCombatDependencies(mock.Object, CreateStatusEffectMediator().Object, FakeRandomSource.Returning(9999, 9999, 100));
        return mock;
    }

    private static PartyCombatTurnHandler CreateHandler(
        Mock<ISaveGameService> saveSvc,
        Mock<CombatService> combatSvc,
        IRandomSource? randomSource = null) =>
        new(
            saveSvc.Object,
            combatSvc.Object,
            new PartyService(NullLogger<PartyService>.Instance),
            new PartyAIService(NullLogger<PartyAIService>.Instance),
            NullLogger<PartyCombatTurnHandler>.Instance,
            randomSource);

    private static Mock<ISaveGameService> CreateSaveService(SaveGame save)
    {
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);
        saveSvc.Setup(s => s.GetDifficultySettings()).Returns(DifficultySettings.Normal);
        return saveSvc;
    }

    private static PartyMember MakeMember(
        string name,
        PartyRole role = PartyRole.DPS,
        int health = 100,
        int maxHealth = 100,
        int strength = 10,
        int dexterity = 0,
        int wisdom = 10,
        int constitution = 10) => new()
        {
            Id = name,
            Name = name,
            Role = role,
            Behavior = AIBehavior.Balanced,
            Health = health,
            MaxHealth = maxHealth,
            Strength = strength,
            Dexterity = dexterity,
            Wisdom = wisdom,
            Constitution = constitution
        };

    private static Enemy MakeEnemy(int health, int basePhysicalDamage = 1) => new()
    {
        Name = "Goblin",
        Health = health,
        MaxHealth = health,
        XP = 20,
        GoldReward = 10,
        Level = 1,
        Strength = 0,
        Constitution = 0,
        BasePhysicalDamage = basePhysicalDamage
    };

    [Fact]
    public async Task Handle_AllyKillsEnemy_ReturnsEnemyDefeatedAndAwardsParty()
    {
        var player = new Character { Name = "Hero", Health = 500, MaxHealth = 500, Gold = 0 };
        var party = new RealmEngine.Shared.Models.Party { Leader = player, Members = [MakeMember("Bruiser", strength: 100)] };
        var save = new SaveGame { PlayerName = "Hero", Character = player, Party = party };
        var saveSvc = CreateSaveService(save);
        var combatSvc = CreateMockCombatService(playerDamage: 1);

        var result = await CreateHandler(saveSvc, combatSvc).Handle(
            new PartyCombatTurnCommand { Enemy = MakeEnemy(health: 5) }, default);

        result.EnemyDefeated.Should().BeTrue();
        result.CombatContinues.Should().BeFalse();
        result.AllyActions.Should().ContainSingle()
            .Which.MemberName.Should().Be("Bruiser");
        result.AllyActions[0].Damage.Should().Be(102, "strength 100 + level 1 * 2, no enemy defence");
        result.XPGained.Should().Be(20);
        result.GoldGained.Should().Be(10);
        player.Gold.Should().Be(10, "party gold is distributed to the leader");
        saveSvc.Verify(s => s.SaveGame(save), Times.Once());
    }

    [Fact]
    public async Task Handle_HealerHealsInjuredLeader_BeforeEnemyTurn()
    {
        var player = new Character { Name = "Hero", Health = 20, MaxHealth = 10000 };
        var healer = MakeMember("Cleric", PartyRole.Healer, wisdom: 10);
        var party = new RealmEngine.Shared.Models.Party { Leader = player, Members = [healer] };
        var save = new SaveGame { PlayerName = "Hero", Character = player, Party = party };
        var saveSvc = CreateSaveService(save);
        var combatSvc = CreateMockCombatService(playerDamage: 1);

        var result = await CreateHandler(saveSvc, combatSvc).Handle(
            new PartyCombatTurnCommand { Enemy = MakeEnemy(health: 10000) }, default);

        result.EnemyDefeated.Should().BeFalse();
        result.AllyActions.Should().ContainSingle()
            .Which.HealAmount.Should().Be(25);
        result.AllyActions[0].MemberName.Should().Be("Cleric");
        result.CombatLog.Should().Contain(m => m.Contains("casts Heal on Hero"));
        result.EnemyTarget.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_EnemyTargetsAlly_TakesTheAllyBranchAndDamagesThatAlly()
    {
        var player = new Character { Name = "Hero", Health = 10_000, MaxHealth = 10_000 };
        var ally = MakeMember("Scout", health: 10_000, maxHealth: 10_000, constitution: 10);
        var party = new RealmEngine.Shared.Models.Party { Leader = player, Members = [ally] };
        var save = new SaveGame { PlayerName = "Hero", Character = player, Party = party };
        var saveSvc = CreateSaveService(save);
        var combatSvc = CreateMockCombatService(playerDamage: 1);

        // 0.9 clears the 60% player-target roll, then index 0 selects the only ally.
        var handler = CreateHandler(saveSvc, combatSvc, FakeRandomSource.ReturningDoubles(0.9).Enqueue(0));

        var result = await handler.Handle(
            new PartyCombatTurnCommand { Enemy = MakeEnemy(health: 10_000) }, default);

        result.EnemyTarget.Should().Be("Scout", "a NextDouble of 0.9 takes the 40% ally-target arm");
        result.EnemyDamage.Should().Be(1, "max(1, enemy base damage - ally defence)");
        result.EnemyMessage.Should().Contain("Scout");
        result.PlayerMessage.Should().NotBeNullOrEmpty();
        ally.Health.Should().Be(9_999);
        player.Health.Should().Be(10_000, "the player is not the target of this turn");
        result.PartyDefeated.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_EnemyTargetsPlayer_WhenThePlayerTargetRollIsTaken()
    {
        var player = new Character { Name = "Hero", Health = 10_000, MaxHealth = 10_000 };
        var ally = MakeMember("Scout", health: 10_000, maxHealth: 10_000, constitution: 10);
        var party = new RealmEngine.Shared.Models.Party { Leader = player, Members = [ally] };
        var save = new SaveGame { PlayerName = "Hero", Character = player, Party = party };
        var saveSvc = CreateSaveService(save);
        var combatSvc = CreateMockCombatService(playerDamage: 1);

        // 0.59 is below the 60% threshold, so the enemy goes for the leader.
        var handler = CreateHandler(saveSvc, combatSvc, FakeRandomSource.ReturningDoubles(0.59));

        var result = await handler.Handle(
            new PartyCombatTurnCommand { Enemy = MakeEnemy(health: 10_000) }, default);

        result.EnemyTarget.Should().Be("Hero");
        result.EnemyDamage.Should().Be(1);
        player.Health.Should().Be(9_999, "ExecuteEnemyAttack applies its damage exactly once; the handler must not subtract it again");
        ally.Health.Should().Be(10_000, "the ally is untouched on the player-target arm");
        result.CombatContinues.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_EnemyAttacksPlayer_WhenThereIsNoParty()
    {
        var player = new Character { Name = "Hero", Health = 10_000, MaxHealth = 10_000 };
        var save = new SaveGame { PlayerName = "Hero", Character = player, Party = null };
        var saveSvc = CreateSaveService(save);
        var combatSvc = CreateMockCombatService(playerDamage: 1);

        var result = await CreateHandler(saveSvc, combatSvc).Handle(
            new PartyCombatTurnCommand { Enemy = MakeEnemy(health: 10_000, basePhysicalDamage: 5) }, default);

        result.EnemyTarget.Should().Be("Hero");
        result.EnemyDamage.Should().Be(1);
        result.EnemyMessage.Should().Contain("Goblin");
        player.Health.Should().Be(9_999, "the no-party arm also lets ExecuteEnemyAttack apply the damage once");
        result.PartyDefeated.Should().BeFalse();
        result.CombatContinues.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_PlayerKillsEnemy_WithAParty_DistributesRewardsThroughTheParty()
    {
        var player = new Character { Name = "Hero", Health = 500, MaxHealth = 500, Gold = 0, Experience = 0 };
        var party = new RealmEngine.Shared.Models.Party { Leader = player, Members = [MakeMember("Scout")] };
        var save = new SaveGame { PlayerName = "Hero", Character = player, Party = party };
        var saveSvc = CreateSaveService(save);
        var combatSvc = CreateMockCombatService(playerDamage: 9999);

        var result = await CreateHandler(saveSvc, combatSvc).Handle(
            new PartyCombatTurnCommand { Enemy = MakeEnemy(health: 10) }, default);

        result.EnemyDefeated.Should().BeTrue();
        result.AllyActions.Should().BeEmpty("the enemy dies before the allies act");
        player.Gold.Should().Be(10, "party gold goes to the leader");
        player.Experience.Should().Be(10, "20 XP is split between the leader and the one ally");
        saveSvc.Verify(s => s.SaveGame(save), Times.Once());
    }

    [Fact]
    public async Task Handle_MarksPartyDefeated_WhenTheEnemyKillsTheSoloPlayer()
    {
        var player = new Character { Name = "Hero", Health = 1, MaxHealth = 100 };
        var save = new SaveGame { PlayerName = "Hero", Character = player, Party = null };
        var saveSvc = CreateSaveService(save);
        var combatSvc = CreateMockCombatService(playerDamage: 1);

        var result = await CreateHandler(saveSvc, combatSvc).Handle(
            new PartyCombatTurnCommand { Enemy = MakeEnemy(health: 10_000, basePhysicalDamage: 1000) }, default);

        result.PartyDefeated.Should().BeTrue();
        result.CombatContinues.Should().BeFalse();
        result.CombatLog.Should().Contain("Your party has been defeated!");
        saveSvc.Verify(s => s.SaveGame(save), Times.Once());
    }
}
