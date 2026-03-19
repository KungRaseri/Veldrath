using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Combat;
using RealmEngine.Core.Features.Party.Commands;
using RealmEngine.Core.Features.Party.Services;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Party;

[Trait("Category", "Feature")]
public class PartyCombatTurnHandlerTests
{
    // CombatService has a protected parameterless ctor for testing.
    // ExecutePlayerAttack is virtual so Moq can intercept it.
    private static Mock<CombatService> CreateMockCombatService(int playerDamage = 9999)
    {
        var mock = new Mock<CombatService>();
        mock.Setup(c => c.ExecutePlayerAttack(
                It.IsAny<Character>(), It.IsAny<Enemy>(), It.IsAny<bool>()))
            .ReturnsAsync(new CombatResult
            {
                Success = true,
                Damage = playerDamage,
                Message = $"Player deals {playerDamage} damage!"
            });
        return mock;
    }

    private static PartyCombatTurnHandler CreateHandler(
        Mock<ISaveGameService>? saveSvc = null,
        Mock<CombatService>? combatSvc = null) =>
        new(
            (saveSvc ?? new Mock<ISaveGameService>()).Object,
            (combatSvc ?? CreateMockCombatService()).Object,
            new PartyService(NullLogger<PartyService>.Instance),
            new PartyAIService(NullLogger<PartyAIService>.Instance),
            NullLogger<PartyCombatTurnHandler>.Instance);

    private static Enemy CreateEnemy(int health = 50) => new()
    {
        Name = "Goblin",
        Health = health,
        MaxHealth = health,
        XP = 25,
        GoldReward = 10,
        Level = 1
    };

    [Fact]
    public async Task Handle_ReturnsCombatEnded_WhenNoActiveSave()
    {
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);

        var result = await CreateHandler(saveSvc).Handle(
            new PartyCombatTurnCommand { Enemy = CreateEnemy(), PlayerAction = "Attack" },
            default);

        result.CombatContinues.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ReturnsEnemyDefeated_WhenPlayerDealsFatalDamage()
    {
        var character = new Character { Name = "Hero", Health = 100, Gold = 0 };
        var save = new SaveGame { PlayerName = "Hero", Character = character, Party = null };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);
        saveSvc.Setup(s => s.GetDifficultySettings())
               .Returns(DifficultySettings.Normal);

        // Lethal damage: enemy has 10 HP, player deals 9999
        var enemy = CreateEnemy(health: 10);

        var result = await CreateHandler(saveSvc).Handle(
            new PartyCombatTurnCommand { Enemy = enemy, PlayerAction = "Attack" },
            default);

        result.EnemyDefeated.Should().BeTrue();
        result.CombatContinues.Should().BeFalse();
        result.XPGained.Should().BeGreaterThan(0);
        result.GoldGained.Should().BeGreaterThan(0);
        saveSvc.Verify(s => s.SaveGame(save), Times.Once);
    }

    [Fact]
    public async Task Handle_AwardsXpAndGoldToPlayer_WhenNoPartyAndEnemyDefeated()
    {
        var character = new Character { Name = "Hero", Health = 100, Gold = 0 };
        var save = new SaveGame { PlayerName = "Hero", Character = character, Party = null };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);
        saveSvc.Setup(s => s.GetDifficultySettings())
               .Returns(new DifficultySettings { GoldXPMultiplier = 2.0 });

        var enemy = new Enemy
        {
            Name = "Orc",
            Health = 10,
            MaxHealth = 10,
            XP = 100,
            GoldReward = 50,
            Level = 3
        };

        var result = await CreateHandler(saveSvc).Handle(
            new PartyCombatTurnCommand { Enemy = enemy, PlayerAction = "Attack" },
            default);

        // With 2x multiplier: 100*2=200 XP, 50*2=100 gold
        result.XPGained.Should().Be(200);
        result.GoldGained.Should().Be(100);
    }
}
