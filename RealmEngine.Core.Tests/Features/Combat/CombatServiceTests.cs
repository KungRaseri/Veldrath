using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Combat;
using RealmEngine.Core.Features.Combat.Commands;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Tests.Infrastructure;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Combat;

[Trait("Category", "Service")]
/// <summary>
/// Tests for CombatService.
/// </summary>
public class CombatServiceTests
{
    private readonly Mock<SaveGameService> _mockSaveGameService;
    private readonly Mock<IMediator> _mockMediator;

    public CombatServiceTests()
    {
        _mockSaveGameService = new Mock<SaveGameService>();
        _mockSaveGameService.Setup(s => s.GetDifficultySettings())
            .Returns(new DifficultySettings
            {
                Name = "Normal",
                EnemyHealthMultiplier = 1.0,
                EnemyDamageMultiplier = 1.0,
                GoldXPMultiplier = 1.0
            });
        _mockMediator = new Mock<IMediator>();

        // Setup mediator to return empty ProcessStatusEffectsResult for all status effect commands
        _mockMediator.Setup(m => m.Send(It.IsAny<ProcessStatusEffectsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessStatusEffectsResult
            {
                TotalDamageTaken = 0,
                TotalHealingReceived = 0,
                TotalStatModifiers = new Dictionary<string, int>(),
                ExpiredEffectTypes = new List<StatusEffectType>()
            });
    }

    /// <summary>
    /// Builds the service under test. Every construction injects a deterministic random source:
    /// a seeded one by default, or a scripted one when the test needs a specific arm.
    /// </summary>
    private CombatService CreateService(IRandomSource? randomSource = null) =>
        new(
            _mockSaveGameService.Object,
            _mockMediator.Object,
            null!,
            NullLogger<CombatService>.Instance,
            NullLoggerFactory.Instance,
            randomSource: randomSource ?? FakeRandomSource.Seeded(20260101));

    [Fact]
    public async Task InitializeCombat_Should_Scale_Enemy_Health_By_Difficulty()
    {
        // Arrange
        _mockSaveGameService.Setup(s => s.GetDifficultySettings())
            .Returns(new DifficultySettings { EnemyHealthMultiplier = 1.5 });
        var service = CreateService();
        var enemy = new Enemy { Name = "Goblin", MaxHealth = 100, Health = 50 };

        // Act
        service.InitializeCombat(enemy);

        // Assert
        enemy.MaxHealth.Should().Be(150); // 100 * 1.5
        enemy.Health.Should().Be(150); // Reset to max
    }

    [Fact]
    public async Task InitializeCombat_Should_Reset_Enemy_Health_To_Max()
    {
        // Arrange
        var service = CreateService();
        var enemy = new Enemy { Name = "Skeleton", MaxHealth = 80, Health = 20 };

        // Act
        service.InitializeCombat(enemy);

        // Assert
        enemy.Health.Should().Be(enemy.MaxHealth);
    }

    [Fact]
    public async Task ExecutePlayerAttack_Should_Deal_Damage_When_The_Enemy_Does_Not_Dodge()
    {
        // Arrange — 9999 on both rolls: no dodge (5%) and no critical (3%); 100 = 100% damage variance.
        var service = CreateService(FakeRandomSource.Returning(9999, 9999, 100));
        var player = new Character { Name = "Warrior", Strength = 15 };
        var enemy = new Enemy { Name = "Orc", Health = 100 };

        // Act
        var result = await service.ExecutePlayerAttack(player, enemy);

        // Assert
        result.IsDodged.Should().BeFalse();
        result.IsCritical.Should().BeFalse();
        result.Damage.Should().BeGreaterThan(0);
        result.Message.Should().Contain("damage");
        enemy.Health.Should().BeLessThan(100);
    }

    [Fact]
    public async Task ExecutePlayerAttack_Should_Report_Critical_Hit_When_The_Critical_Roll_Succeeds()
    {
        // Arrange — no dodge, then a 0 critical roll (always below the crit chance), 100 = 100% variance.
        var service = CreateService(FakeRandomSource.Returning(9999, 0, 100));
        var player = new Character { Name = "Warrior", Strength = 15 };
        var enemy = new Enemy { Name = "Orc", Health = 500 };

        // Act
        var result = await service.ExecutePlayerAttack(player, enemy);

        // Assert
        result.IsCritical.Should().BeTrue();
        result.Message.Should().Contain("CRITICAL HIT");
    }

    [Fact]
    public async Task ExecutePlayerAttack_Should_Report_Dodge_When_The_Enemy_Dodges()
    {
        // Arrange — a 0 dodge roll is below any non-zero dodge chance.
        var service = CreateService(FakeRandomSource.Returning(0));
        var player = new Character { Name = "Warrior", Strength = 15 };
        var enemy = new Enemy { Name = "Orc", Health = 100 };

        // Act
        var result = await service.ExecutePlayerAttack(player, enemy);

        // Assert
        result.IsDodged.Should().BeTrue();
        result.Damage.Should().Be(0);
        result.Message.Should().Contain("dodged");
        enemy.Health.Should().Be(100);
    }

    [Fact]
    public async Task ExecuteEnemyAttack_Should_Apply_Defense_Reduction_When_Defending()
    {
        // Arrange — deterministic rolls: no dodge (9999), no block (99), no crit (9999), 100% variance.
        var normalService = CreateService(FakeRandomSource.Returning(9999, 9999, 100));
        var defendingService = CreateService(FakeRandomSource.Returning(9999, 99, 9999, 100));

        var normalPlayer = new Character { Name = "Hero", Health = 100, MaxHealth = 100, Constitution = 10, Dexterity = 5 };
        var defendingPlayer = new Character { Name = "Defender", Health = 100, MaxHealth = 100, Constitution = 10, Dexterity = 5 };
        var normalEnemy = new Enemy { Name = "AttackerA", BasePhysicalDamage = 20 };
        var defendingEnemy = new Enemy { Name = "AttackerB", BasePhysicalDamage = 20 };

        // Act
        var normalResult = await normalService.ExecuteEnemyAttack(normalEnemy, normalPlayer, isDefending: false);
        var defendingResult = await defendingService.ExecuteEnemyAttack(defendingEnemy, defendingPlayer, isDefending: true);

        // Assert
        normalResult.IsBlocked.Should().BeFalse();
        defendingResult.IsBlocked.Should().BeFalse();
        defendingResult.Damage.Should().BeLessThanOrEqualTo(normalResult.Damage,
            "halving the enemy's damage before defense should never increase the damage taken");
    }

    [Fact]
    public async Task ExecuteEnemyAttack_Should_Report_Block_When_Defending_And_The_Block_Roll_Succeeds()
    {
        // Arrange — no dodge (9999), then a 0 block roll (always below the 50% block chance).
        var service = CreateService(FakeRandomSource.Returning(9999, 0));
        var player = new Character { Name = "Hero", Health = 100, MaxHealth = 100 };
        var enemy = new Enemy { Name = "Orc", BasePhysicalDamage = 10 };

        // Act
        var result = await service.ExecuteEnemyAttack(enemy, player, isDefending: true);

        // Assert
        result.IsBlocked.Should().BeTrue();
        result.Damage.Should().Be(0);
        result.Message.Should().Contain("blocked");
    }

    [Fact]
    public async Task ExecuteEnemyAttack_Should_Report_Dodge_When_The_Player_Dodges()
    {
        // Arrange — a 0 dodge roll is below any non-zero dodge chance.
        var service = CreateService(FakeRandomSource.Returning(0));
        var player = new Character { Name = "Hero", Health = 100, MaxHealth = 100 };
        var enemy = new Enemy { Name = "Orc", BasePhysicalDamage = 10 };

        // Act
        var result = await service.ExecuteEnemyAttack(enemy, player);

        // Assert
        result.IsDodged.Should().BeTrue();
        result.Damage.Should().Be(0);
        result.Message.Should().Contain("dodged");
        player.Health.Should().Be(100);
    }

    [Fact]
    public async Task ExecuteEnemyAttack_Should_Not_Reduce_Health_Below_Zero()
    {
        // Arrange — no dodge (9999), no crit (9999), 100% damage variance.
        var service = CreateService(FakeRandomSource.Returning(9999, 9999, 100));
        var player = new Character { Name = "Hero", Health = 1, MaxHealth = 100 };
        var enemy = new Enemy { Name = "Dragon", BasePhysicalDamage = 1000 };

        // Act
        await service.ExecuteEnemyAttack(enemy, player);

        // Assert - Minimum damage is 1, so should go to 0 (not negative)
        player.Health.Should().Be(0);
    }

    [Fact]
    public async Task AttemptFlee_Should_Succeed_When_The_Roll_Is_Below_The_Chance()
    {
        // Arrange — player DEX 10 vs enemy DEX 5 = 75% chance; a 0 roll always succeeds.
        var service = CreateService(FakeRandomSource.Returning(0));
        var player = new Character { Name = "Hero", Dexterity = 10 };
        var weakEnemy = new Enemy { Name = "Rat", Level = 1, Dexterity = 5 };

        // Act
        var result = service.AttemptFlee(player, weakEnemy);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("successfully fled");
    }

    [Fact]
    public async Task AttemptFlee_Should_Fail_When_The_Roll_Is_At_Or_Above_The_Chance()
    {
        // Arrange — player DEX 10 vs enemy DEX 15 = 25% chance; a 99 roll always fails.
        var service = CreateService(FakeRandomSource.Returning(99));
        var player = new Character { Name = "Hero", Dexterity = 10 };
        var strongEnemy = new Enemy { Name = "Dragon", Level = 20, Dexterity = 15 };

        // Act
        var result = service.AttemptFlee(player, strongEnemy);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Failed to escape");
    }

    [Fact]
    public async Task UseItemInCombat_Should_Apply_Healing()
    {
        // Arrange
        var service = CreateService();
        var player = new Character { Name = "Hero", Health = 50, MaxHealth = 100 };
        var potion = new Item { Name = "Health Potion", Type = ItemType.Consumable };

        // Act
        var result = service.UseItemInCombat(player, potion);

        // Assert
        result.Success.Should().BeTrue();
        player.Health.Should().BeGreaterThan(50);
    }

    [Fact]
    public async Task UseItemInCombat_Should_Fail_For_Non_Consumables()
    {
        // Arrange
        var service = CreateService();
        var player = new Character { Name = "Hero", Health = 50, MaxHealth = 100 };
        var weapon = new Item { Name = "Sword", Type = ItemType.Weapon };

        // Act
        var result = service.UseItemInCombat(player, weapon);

        // Assert
        result.Success.Should().BeFalse();
        player.Health.Should().Be(50); // No healing
    }

    [Fact]
    public async Task GenerateVictoryOutcome_Should_Award_XP_And_Gold()
    {
        // Arrange
        var service = CreateService();
        var player = new Character { Name = "Hero" };
        var enemy = new Enemy { Name = "Goblin", XP = 50, GoldReward = 25 };

        // Act
        var outcome = await service.GenerateVictoryOutcome(player, enemy);

        // Assert
        outcome.PlayerVictory.Should().BeTrue();
        outcome.XPGained.Should().Be(50);
        outcome.GoldGained.Should().Be(25);
        outcome.Summary.Should().Contain("Victory");
        outcome.Summary.Should().Contain("50 XP");
        outcome.Summary.Should().Contain("25 Gold");
    }

    [Fact]
    public async Task GenerateVictoryOutcome_Should_Include_Enemy_Name_In_Summary()
    {
        // Arrange
        var service = CreateService();
        var player = new Character { Name = "Hero" };
        var enemy = new Enemy { Name = "Skeleton King", XP = 100, GoldReward = 50 };

        // Act
        var outcome = await service.GenerateVictoryOutcome(player, enemy);

        // Assert
        outcome.Summary.Should().Contain("Skeleton King");
    }

    [Theory]
    [InlineData(0.5, 50)]
    [InlineData(1.0, 100)]
    [InlineData(2.0, 200)]
    public void InitializeCombat_Should_Scale_Health_Correctly(double multiplier, int expectedHealth)
    {
        // Arrange
        _mockSaveGameService.Setup(s => s.GetDifficultySettings())
            .Returns(new DifficultySettings { EnemyHealthMultiplier = multiplier });
        var service = CreateService();
        var enemy = new Enemy { Name = "TestEnemy", MaxHealth = 100, Health = 100 };

        // Act
        service.InitializeCombat(enemy);

        // Assert
        enemy.MaxHealth.Should().Be(expectedHealth);
        enemy.Health.Should().Be(expectedHealth);
    }

    [Fact]
    public async Task ExecuteEnemyAttack_Should_Include_Message()
    {
        // Arrange — no dodge (9999), no crit (9999), 100% damage variance.
        var service = CreateService(FakeRandomSource.Returning(9999, 9999, 100));
        var player = new Character { Name = "Hero", Health = 100, MaxHealth = 100 };
        var enemy = new Enemy { Name = "Orc", BasePhysicalDamage = 10 };

        // Act
        var result = await service.ExecuteEnemyAttack(enemy, player);

        // Assert
        result.Message.Should().NotBeNullOrEmpty();
        result.Message.Should().Contain("Orc");
    }
}
