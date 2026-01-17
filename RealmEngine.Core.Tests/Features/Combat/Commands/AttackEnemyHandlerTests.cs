using FluentAssertions;
using Moq;
using RealmEngine.Shared.Models;
using RealmEngine.Core.Features.Combat;
using RealmEngine.Core.Features.Combat.Commands.AttackEnemy;
using RealmEngine.Core.Features.SaveLoad;
using MediatR;

namespace RealmEngine.Core.Tests.Features.Combat.Commands;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for AttackEnemyHandler.
/// </summary>
public class AttackEnemyHandlerTests
{
    private readonly Mock<CombatService> _mockCombatService;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<SaveGameService> _mockSaveGameService;
    private readonly AttackEnemyHandler _handler;

    public AttackEnemyHandlerTests()
    {
        _mockCombatService = new Mock<CombatService>();
        _mockMediator = new Mock<IMediator>();
        _mockSaveGameService = new Mock<SaveGameService>();
        _handler = new AttackEnemyHandler(_mockCombatService.Object, _mockMediator.Object, _mockSaveGameService.Object);
    }

    [Fact]
    public async Task Handle_Should_Deal_Damage_To_Enemy()
    {
        // Arrange
        var player = new Character { Name = "Hero", Strength = 10, Level = 1 };
        var enemy = new Enemy { Name = "Goblin", Health = 50, MaxHealth = 50 };
        var combatResult = new CombatResult { Damage = 15, IsCritical = false };

        _mockCombatService.Setup(s => s.ExecutePlayerAttack(player, enemy, false)).ReturnsAsync(combatResult);
        
        _mockSaveGameService.Setup(s => s.GetDifficultySettings())
            .Returns(new DifficultySettings { GoldXPMultiplier = 1.0 });

        var command = new AttackEnemyCommand { Player = player, Enemy = enemy, CombatLog = null };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Damage.Should().Be(15);
        result.IsCritical.Should().BeFalse();
        result.IsEnemyDefeated.Should().BeFalse();
        enemy.Health.Should().Be(35);
    }

    [Fact]
    public async Task Handle_Should_Mark_Enemy_As_Defeated_When_Health_Reaches_Zero()
    {
        // Arrange
        var player = new Character { Name = "Hero", Strength = 20, Experience = 0, Gold = 0 };
        var enemy = new Enemy { Name = "Goblin", Health = 10, MaxHealth = 50, XP = 25, GoldReward = 10 };
        var combatResult = new CombatResult { Damage = 15, IsCritical = false };

        _mockCombatService.Setup(s => s.ExecutePlayerAttack(player, enemy, false)).ReturnsAsync(combatResult);
        
        _mockSaveGameService.Setup(s => s.GetDifficultySettings())
            .Returns(new DifficultySettings { GoldXPMultiplier = 1.0 });

        var command = new AttackEnemyCommand { Player = player, Enemy = enemy, CombatLog = null };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsEnemyDefeated.Should().BeTrue();
        result.ExperienceGained.Should().Be(25);
        result.GoldGained.Should().Be(10);
        player.Experience.Should().Be(25);
        player.Gold.Should().Be(10);
        enemy.Health.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Should_Apply_Difficulty_Multiplier_To_Rewards()
    {
        // Arrange
        var player = new Character { Name = "Hero", Strength = 20, Experience = 0, Gold = 0 };
        var enemy = new Enemy { Name = "Dragon", Health = 5, MaxHealth = 100, XP = 100, GoldReward = 50 };
        var combatResult = new CombatResult { Damage = 50, IsCritical = false };

        _mockCombatService.Setup(s => s.ExecutePlayerAttack(player, enemy, false)).ReturnsAsync(combatResult);
        
        _mockSaveGameService.Setup(s => s.GetDifficultySettings())
            .Returns(new DifficultySettings { GoldXPMultiplier = 1.5 });

        var command = new AttackEnemyCommand { Player = player, Enemy = enemy, CombatLog = null };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ExperienceGained.Should().Be(150); // 100 * 1.5
        result.GoldGained.Should().Be(75); // 50 * 1.5
        player.Experience.Should().Be(150);
        player.Gold.Should().Be(75);
    }

    [Fact]
    public async Task Handle_Should_Record_Critical_Hits()
    {
        // Arrange
        var player = new Character { Name = "Hero", Strength = 15 };
        var enemy = new Enemy { Name = "Orc", Health = 100, MaxHealth = 100 };
        var combatResult = new CombatResult { Damage = 30, IsCritical = true };

        _mockCombatService.Setup(s => s.ExecutePlayerAttack(player, enemy, false)).ReturnsAsync(combatResult);
        
        _mockSaveGameService.Setup(s => s.GetDifficultySettings())
            .Returns(new DifficultySettings { GoldXPMultiplier = 1.0 });

        var command = new AttackEnemyCommand { Player = player, Enemy = enemy, CombatLog = null };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsCritical.Should().BeTrue();
        result.Damage.Should().Be(30);
        enemy.Health.Should().Be(70);
    }

    [Fact]
    public async Task Handle_Should_Publish_Attack_Performed_Event()
    {
        // Arrange
        var player = new Character { Name = "Hero", Strength = 10 };
        var enemy = new Enemy { Name = "Skeleton", Health = 50, MaxHealth = 50 };
        var combatResult = new CombatResult { Damage = 12, IsCritical = false };

        _mockCombatService.Setup(s => s.ExecutePlayerAttack(player, enemy, false)).ReturnsAsync(combatResult);
        
        _mockSaveGameService.Setup(s => s.GetDifficultySettings())
            .Returns(new DifficultySettings { GoldXPMultiplier = 1.0 });

        var command = new AttackEnemyCommand { Player = player, Enemy = enemy, CombatLog = null };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockMediator.Verify(m => m.Publish(
            It.Is<AttackPerformed>(e => e.AttackerName == "Hero" && e.DefenderName == "Skeleton" && e.Damage == 12),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Publish_Enemy_Defeated_Event_When_Enemy_Dies()
    {
        // Arrange
        var player = new Character { Name = "Hero", Strength = 20, Experience = 0, Gold = 0 };
        var enemy = new Enemy { Name = "Rat", Health = 5, MaxHealth = 10, XP = 5, GoldReward = 2 };
        var combatResult = new CombatResult { Damage = 10, IsCritical = false };

        _mockCombatService.Setup(s => s.ExecutePlayerAttack(player, enemy, false)).ReturnsAsync(combatResult);
        
        _mockSaveGameService.Setup(s => s.GetDifficultySettings())
            .Returns(new DifficultySettings { GoldXPMultiplier = 1.0 });

        var command = new AttackEnemyCommand { Player = player, Enemy = enemy, CombatLog = null };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockMediator.Verify(m => m.Publish(
            It.Is<EnemyDefeated>(e => e.PlayerName == "Hero" && e.EnemyName == "Rat"),
            It.IsAny<CancellationToken>()), Times.Once);
        
        _mockMediator.Verify(m => m.Publish(
            It.Is<GoldGained>(e => e.PlayerName == "Hero" && e.Amount == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Add_Combat_Log_Entries()
    {
        // Arrange
        var player = new Character { Name = "Warrior", Strength = 12 };
        var enemy = new Enemy { Name = "Bandit", Health = 40, MaxHealth = 40 };
        var combatLog = new CombatLog();
        var combatResult = new CombatResult { Damage = 18, IsCritical = true };

        _mockCombatService.Setup(s => s.ExecutePlayerAttack(player, enemy, false)).ReturnsAsync(combatResult);
        
        _mockSaveGameService.Setup(s => s.GetDifficultySettings())
            .Returns(new DifficultySettings { GoldXPMultiplier = 1.0 });

        var command = new AttackEnemyCommand { Player = player, Enemy = enemy, CombatLog = combatLog };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        combatLog.Entries.Should().Contain(e => e.Message.Contains("Warrior attacks for 18 damage (CRITICAL!)"));
    }

    [Fact]
    public async Task Handle_Should_Not_Award_Negative_Health_To_Enemy()
    {
        // Arrange
        var player = new Character { Name = "Hero", Strength = 50 };
        var enemy = new Enemy { Name = "Slime", Health = 5, MaxHealth = 10 };
        var combatResult = new CombatResult { Damage = 100, IsCritical = false };

        _mockCombatService.Setup(s => s.ExecutePlayerAttack(player, enemy, false)).ReturnsAsync(combatResult);
        
        _mockSaveGameService.Setup(s => s.GetDifficultySettings())
            .Returns(new DifficultySettings { GoldXPMultiplier = 1.0 });

        var command = new AttackEnemyCommand { Player = player, Enemy = enemy, CombatLog = null };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        enemy.Health.Should().Be(0);
        enemy.Health.Should().BeGreaterThanOrEqualTo(0);
    }

    [Theory]
    [InlineData(10, 100, false)] // Normal hit, enemy survives
    [InlineData(50, 50, true)]   // Normal hit, enemy defeated
    [InlineData(25, 30, false)]  // Normal hit, enemy low health
    public async Task Handle_Should_Calculate_Defeat_Status_Correctly(int damage, int initialHealth, bool expectedDefeat)
    {
        // Arrange
        var player = new Character { Name = "Hero", Strength = 10, Experience = 0, Gold = 0 };
        var enemy = new Enemy 
        { 
            Name = "TestEnemy", 
            Health = initialHealth, 
            MaxHealth = 100, 
            XPReward = 10, 
            GoldReward = 5 
        };
        var combatResult = new CombatResult { Damage = damage, IsCritical = false };

        _mockCombatService.Setup(s => s.ExecutePlayerAttack(player, enemy, false)).ReturnsAsync(combatResult);
        
        _mockSaveGameService.Setup(s => s.GetDifficultySettings())
            .Returns(new DifficultySettings { GoldXPMultiplier = 1.0 });

        var command = new AttackEnemyCommand { Player = player, Enemy = enemy, CombatLog = null };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsEnemyDefeated.Should().Be(expectedDefeat);
    }
}



