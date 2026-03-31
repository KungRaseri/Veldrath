using FluentAssertions;
using RealmEngine.Core.Features.Combat.Queries.GetEnemyInfo;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Combat.Queries;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for GetEnemyInfoHandler.
/// </summary>
public class GetEnemyInfoHandlerTests
{
    [Fact]
    public async Task Handle_Should_Return_Enemy_Info()
    {
        // Arrange
        var handler = new GetEnemyInfoHandler();
        var enemy = new Enemy
        {
            Name = "Goblin",
            Level = 5,
            Health = 50,
            MaxHealth = 100,
            Strength = 12,
            Constitution = 10,
            Difficulty = EnemyDifficulty.Easy
        };
        var query = new GetEnemyInfoQuery { Enemy = enemy };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Goblin");
        result.Level.Should().Be(5);
        result.Health.Should().Be(50);
        result.MaxHealth.Should().Be(100);
    }

    [Fact]
    public async Task Handle_Should_Return_Attack_From_BasePhysicalDamage()
    {
        // Arrange
        var handler = new GetEnemyInfoHandler();
        var enemy = new Enemy
        {
            Name = "Orc",
            BasePhysicalDamage = 15
        };
        var query = new GetEnemyInfoQuery { Enemy = enemy };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Attack.Should().Be(15);
    }

    [Fact]
    public async Task Handle_Should_Return_Defense_From_Calculation()
    {
        // Arrange
        var handler = new GetEnemyInfoHandler();
        var enemy = new Enemy
        {
            Name = "Dragon",
            Constitution = 20
        };
        var query = new GetEnemyInfoQuery { Enemy = enemy };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Defense.Should().Be(enemy.GetPhysicalDefense());
    }

    [Fact]
    public async Task Handle_Should_Return_Difficulty_Level()
    {
        // Arrange
        var handler = new GetEnemyInfoHandler();
        var enemy = new Enemy
        {
            Name = "Boss",
            Difficulty = EnemyDifficulty.Hard
        };
        var query = new GetEnemyInfoQuery { Enemy = enemy };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Difficulty.Should().Be(EnemyDifficulty.Hard);
    }

    [Fact]
    public async Task Handle_Should_Generate_Description_With_Level_And_Difficulty()
    {
        // Arrange
        var handler = new GetEnemyInfoHandler();
        var enemy = new Enemy
        {
            Name = "Skeleton",
            Level = 10,
            Difficulty = EnemyDifficulty.Normal
        };
        var query = new GetEnemyInfoQuery { Enemy = enemy };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Description.Should().Contain("level 10");
        result.Description.Should().Contain("normal");
    }

    [Theory]
    [InlineData(EnemyDifficulty.Easy, "easy")]
    [InlineData(EnemyDifficulty.Normal, "normal")]
    [InlineData(EnemyDifficulty.Hard, "hard")]
    [InlineData(EnemyDifficulty.Elite, "elite")]
    [InlineData(EnemyDifficulty.Boss, "boss")]
    public async Task Handle_Should_Format_Difficulty_In_Description(EnemyDifficulty difficulty, string expectedText)
    {
        // Arrange
        var handler = new GetEnemyInfoHandler();
        var enemy = new Enemy
        {
            Name = "Test Enemy",
            Level = 1,
            Difficulty = difficulty
        };
        var query = new GetEnemyInfoQuery { Enemy = enemy };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Description.Should().ContainEquivalentOf(expectedText);
    }
}
