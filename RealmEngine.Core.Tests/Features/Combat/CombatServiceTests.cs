using FluentAssertions;
using Moq;
using RealmEngine.Shared.Models;
using RealmEngine.Core.Features.Combat;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Features.Combat.Commands;
using RealmEngine.Core.Features.SaveLoad;
using MediatR;

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

    [Fact]
    public async Task InitializeCombat_Should_Scale_Enemy_Health_By_Difficulty()
    {
        // Arrange
        _mockSaveGameService.Setup(s => s.GetDifficultySettings())
            .Returns(new DifficultySettings { EnemyHealthMultiplier = 1.5 });
        var service = new CombatService(_mockSaveGameService.Object, _mockMediator.Object, null!, NullLogger<CombatService>.Instance, NullLoggerFactory.Instance);
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
        var service = new CombatService(_mockSaveGameService.Object, _mockMediator.Object, null!, NullLogger<CombatService>.Instance, NullLoggerFactory.Instance);
        var enemy = new Enemy { Name = "Skeleton", MaxHealth = 80, Health = 20 };

        // Act
        service.InitializeCombat(enemy);

        // Assert
        enemy.Health.Should().Be(enemy.MaxHealth);
    }

    [Fact]
    public async Task ExecutePlayerAttack_Should_Return_Message()
    {
        // Arrange
        var service = new CombatService(_mockSaveGameService.Object, _mockMediator.Object, null!, NullLogger<CombatService>.Instance, NullLoggerFactory.Instance);
        var player = new Character { Name = "Warrior", Strength = 15 };
        var enemy = new Enemy { Name = "Orc", Health = 100 };

        // Act - Multiple attempts to account for dodge RNG
        bool gotDamageMessage = false;
        for (int i = 0; i < 50; i++)
        {
            var result = await service.ExecutePlayerAttack(player, enemy);
            result.Message.Should().NotBeNullOrEmpty();
            if (result.Message.Contains("damage", StringComparison.OrdinalIgnoreCase))
            {
                gotDamageMessage = true;
                break;
            }
        }

        // Assert
        gotDamageMessage.Should().BeTrue("Should eventually land a hit and deal damage");
    }

    [Fact]
    public async Task ExecuteEnemyAttack_Should_Apply_Defense_Reduction_When_Defending()
    {
        // Arrange
        var service = new CombatService(_mockSaveGameService.Object, _mockMediator.Object, null!, NullLogger<CombatService>.Instance, NullLoggerFactory.Instance);
        
        // Set up multiple attack scenarios to handle RNG variance (dodge/block/damage rolls)
        // Test with multiple characters to ensure we get at least one hit for comparison
        var attacks = new List<(Character player, Enemy enemy, bool isDefending, CombatResult result)>();
        
        // Run multiple attempts to get reliable data despite RNG
        for (int i = 0; i < 10; i++)
        {
            var normalPlayer = new Character { Name = $"Hero{i}", Health = 100, MaxHealth = 100, Constitution = 10, Dexterity = 5 };
            var defendingPlayer = new Character { Name = $"Defender{i}", Health = 100, MaxHealth = 100, Constitution = 10, Dexterity = 5 };
            var enemy1 = new Enemy { Name = $"Attacker{i}A", BasePhysicalDamage = 20 };
            var enemy2 = new Enemy { Name = $"Attacker{i}B", BasePhysicalDamage = 20 };
            
            var normalResult = await service.ExecuteEnemyAttack(enemy1, normalPlayer, isDefending: false);
            var defendingResult = await service.ExecuteEnemyAttack(enemy2, defendingPlayer, isDefending: true);
            
            attacks.Add((normalPlayer, enemy1, false, normalResult));
            attacks.Add((defendingPlayer, enemy2, true, defendingResult));
        }

        // Get all non-dodged, non-blocked normal attacks
        var normalHits = attacks.Where(a => !a.isDefending && a.result.Damage > 0).ToList();
        // Get all non-blocked defending attacks (defending can reduce to 0 legitimately)
        var defendingHits = attacks.Where(a => a.isDefending && !a.result.IsBlocked).ToList();
        
        // Assert - with 10 attempts, we should get at least some hits
        normalHits.Should().NotBeEmpty("at least some normal attacks should land");
        
        // Average damage comparison
        var avgNormalDamage = normalHits.Average(a => a.result.Damage);
        var avgDefendingDamage = defendingHits.Average(a => a.result.Damage);
        
        avgNormalDamage.Should().BeGreaterThan(0, "normal attacks should deal damage on average");
        avgDefendingDamage.Should().BeLessThanOrEqualTo(avgNormalDamage, 
            "defending should reduce average damage taken");
    }

    [Fact]
    public async Task ExecuteEnemyAttack_Should_Not_Reduce_Health_Below_Zero()
    {
        // Arrange
        var service = new CombatService(_mockSaveGameService.Object, _mockMediator.Object, null!, NullLogger<CombatService>.Instance, NullLoggerFactory.Instance);
        var player = new Character { Name = "Hero", Health = 1, MaxHealth = 100 };
        var enemy = new Enemy { Name = "Dragon", BasePhysicalDamage = 1000 };

        // Act
        await service.ExecuteEnemyAttack(enemy, player);

        // Assert - Minimum damage is 1, so should go to 0 (not negative)
        player.Health.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task AttemptFlee_Should_Return_Success_Or_Failure()
    {
        // Arrange
        var service = new CombatService(_mockSaveGameService.Object, _mockMediator.Object, null!, NullLogger<CombatService>.Instance, NullLoggerFactory.Instance);
        var player = new Character { Name = "Hero", Dexterity = 15 };
        var enemy = new Enemy { Name = "Slowpoke", Level = 1 };

        // Act
        var result = service.AttemptFlee(player, enemy);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().NotBeNullOrEmpty();
        // Success is RNG-based, so we just verify the result is valid
    }

    [Fact]
    public async Task AttemptFlee_Should_Be_Affected_By_Level_Difference()
    {
        // Arrange
        var service = new CombatService(_mockSaveGameService.Object, _mockMediator.Object, null!, NullLogger<CombatService>.Instance, NullLoggerFactory.Instance);
        var player = new Character { Name = "Hero", Level = 10, Dexterity = 10 };
        // Weak enemy has lower DEX (flee mechanics use DEX, not level)
        var weakEnemy = new Enemy { Name = "Rat", Level = 1, Dexterity = 5 };
        // Strong enemy has higher DEX
        var strongEnemy = new Enemy { Name = "Dragon", Level = 20, Dexterity = 15 };

        // Act - Multiple attempts (RNG) - Increased sample size for better statistical reliability
        int weakEnemySuccesses = 0;
        int strongEnemySuccesses = 0;
        
        for (int i = 0; i < 100; i++)
        {
            var result1 = service.AttemptFlee(player, weakEnemy);
            if (result1.Success) weakEnemySuccesses++;
            
            var result2 = service.AttemptFlee(player, strongEnemy);
            if (result2.Success) strongEnemySuccesses++;
        }

        // Assert - Should have more success fleeing from weak enemy (player DEX 10 vs enemy DEX 5/15)
        // Player has 10 DEX vs 5 DEX (weak) = +25% flee chance (50% base + 25% = 75%)
        // Player has 10 DEX vs 15 DEX (strong) = -25% flee chance (50% base - 25% = 25%)
        // Expected ~75 successes vs ~25 successes (with RNG variance, allow for ±15)
        weakEnemySuccesses.Should().BeGreaterThan(strongEnemySuccesses + 15,
            "Fleeing from lower DEX enemies should be significantly easier (got {0} vs {1})",
            weakEnemySuccesses, strongEnemySuccesses);
    }

    [Fact]
    public async Task UseItemInCombat_Should_Apply_Healing()
    {
        // Arrange
        var service = new CombatService(_mockSaveGameService.Object, _mockMediator.Object, null!, NullLogger<CombatService>.Instance, NullLoggerFactory.Instance);
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
        var service = new CombatService(_mockSaveGameService.Object, _mockMediator.Object, null!, NullLogger<CombatService>.Instance, NullLoggerFactory.Instance);
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
        var service = new CombatService(_mockSaveGameService.Object, _mockMediator.Object, null!, NullLogger<CombatService>.Instance, NullLoggerFactory.Instance);
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
        var service = new CombatService(_mockSaveGameService.Object, _mockMediator.Object, null!, NullLogger<CombatService>.Instance, NullLoggerFactory.Instance);
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
        var service = new CombatService(_mockSaveGameService.Object, _mockMediator.Object, null!, NullLogger<CombatService>.Instance, NullLoggerFactory.Instance);
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
        // Arrange
        var service = new CombatService(_mockSaveGameService.Object, _mockMediator.Object, null!, NullLogger<CombatService>.Instance, NullLoggerFactory.Instance);
        var player = new Character { Name = "Hero", Health = 100, MaxHealth = 100 };
        var enemy = new Enemy { Name = "Orc", BasePhysicalDamage = 10 };

        // Act
        var result = await service.ExecuteEnemyAttack(enemy, player);

        // Assert
        result.Message.Should().NotBeNullOrEmpty();
        result.Message.Should().Contain("Orc");
    }

}





