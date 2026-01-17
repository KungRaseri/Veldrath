using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Data.Services;
using Xunit;

namespace RealmEngine.Core.Tests.Generators;

[Trait("Category", "Generator")]
public class EnemyGeneratorTests
{
    private readonly GameDataCache _dataCache;
    private readonly ReferenceResolverService _referenceResolver;
    private readonly EnemyGenerator _generator;

    public EnemyGeneratorTests()
    {
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "RealmEngine.Data", "Data", "Json");
        _dataCache = new GameDataCache(basePath);
        var mockLogger = new Mock<ILogger<ReferenceResolverService>>();
        _referenceResolver = new ReferenceResolverService(_dataCache, mockLogger.Object);
        var enemyLogger = new Mock<ILogger<EnemyGenerator>>();
        _generator = new EnemyGenerator(_dataCache, _referenceResolver, enemyLogger.Object);
    }

    [Fact]
    public async Task Should_Generate_Enemies_From_Category()
    {
        // Arrange
        _dataCache.LoadAllData();

        // Act
        var enemies = await _generator.GenerateEnemiesAsync("beasts", 5);

        // Assert
        enemies.Should().NotBeNull();
        enemies.Should().HaveCount(5);
        enemies.Should().AllSatisfy(enemy =>
        {
            enemy.Name.Should().NotBeNullOrEmpty();
            enemy.Id.Should().Contain("beasts:");
        });
    }

    [Theory]
    [InlineData("beasts")]
    [InlineData("undead")]
    [InlineData("dragons")]
    public async Task Should_Generate_Enemies_From_Different_Categories(string category)
    {
        // Arrange
        _dataCache.LoadAllData();

        // Act
        var enemies = await _generator.GenerateEnemiesAsync(category, 3);

        // Assert
        enemies.Should().NotBeNull();
        enemies.Should().HaveCountGreaterThan(0);
        enemies.Should().AllSatisfy(enemy =>
        {
            enemy.Id.Should().Contain($"{category}:");
        });
    }

    [Fact]
    public async Task Should_Generate_Enemy_By_Name()
    {
        // Arrange
        _dataCache.LoadAllData();

        // Act
        var enemy = await _generator.GenerateEnemyByNameAsync("beasts", "Wolf");

        // Assert
        enemy.Should().NotBeNull();
        enemy!.Name.Should().NotBeNullOrWhiteSpace("enemy should have a pattern-generated name");
        enemy.Id.Should().Contain("beasts:");
    }

    [Fact]
    public async Task Should_Return_Null_For_Non_Existent_Enemy()
    {
        // Arrange
        _dataCache.LoadAllData();

        // Act
        var enemy = await _generator.GenerateEnemyByNameAsync("beasts", "NonExistentEnemy");

        // Assert
        enemy.Should().BeNull();
    }

    [Fact]
    public async Task Should_Generate_Enemies_With_Valid_Stats()
    {
        // Arrange
        _dataCache.LoadAllData();

        // Act
        var enemies = await _generator.GenerateEnemiesAsync("beasts", 10);

        // Assert
        enemies.Should().AllSatisfy(enemy =>
        {
            // Basic properties
            enemy.Health.Should().BeGreaterThan(0);
            enemy.MaxHealth.Should().BeGreaterThan(0);
            enemy.Level.Should().BeGreaterThan(0);
            
            // Stats should be reasonable
            enemy.Strength.Should().BeInRange(1, 30);
            enemy.Dexterity.Should().BeInRange(1, 30);
            enemy.Constitution.Should().BeInRange(1, 30);
            enemy.Intelligence.Should().BeInRange(1, 30);
            enemy.Wisdom.Should().BeInRange(1, 30);
            enemy.Charisma.Should().BeInRange(1, 30);
            
            // Rewards
            enemy.XP.Should().BeGreaterThan(0);
            enemy.GoldReward.Should().BeGreaterThanOrEqualTo(0);
        });
    }

    [Fact]
    public async Task Should_Generate_Enemies_With_Combat_Stats()
    {
        // Arrange
        _dataCache.LoadAllData();

        // Act
        var enemies = await _generator.GenerateEnemiesAsync("beasts", 10);

        // Assert
        enemies.Should().AllSatisfy(enemy =>
        {
            enemy.BasePhysicalDamage.Should().BeGreaterThan(0);
            enemy.BaseMagicDamage.Should().BeGreaterThanOrEqualTo(0);
        });
    }

    [Fact]
    public async Task Should_Handle_Empty_Category_Gracefully()
    {
        // Arrange
        _dataCache.LoadAllData();

        // Act
        var enemies = await _generator.GenerateEnemiesAsync("nonexistentcategory", 5);

        // Assert
        enemies.Should().NotBeNull();
        enemies.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_Generate_Different_Enemies_From_Same_Category()
    {
        // Arrange
        _dataCache.LoadAllData();

        // Act
        var enemies1 = await _generator.GenerateEnemiesAsync("beasts", 20);
        var enemies2 = await _generator.GenerateEnemiesAsync("beasts", 20);

        // Assert
        var uniqueNames = enemies1.Select(e => e.Name).Distinct().ToList();
        uniqueNames.Should().HaveCountGreaterThan(1, "should generate variety of enemies");
    }

    [Fact]
    public async Task Should_Set_Proper_Enemy_Id_Format()
    {
        // Arrange
        _dataCache.LoadAllData();

        // Act
        var enemies = await _generator.GenerateEnemiesAsync("undead", 5);

        // Assert
        enemies.Should().AllSatisfy(enemy =>
        {
            enemy.Id.Should().MatchRegex(@"^undead:.+$");
        });
    }

    [Fact]
    public async Task Should_Generate_Enemies_With_Descriptions()
    {
        // Arrange
        _dataCache.LoadAllData();

        // Act
        var enemies = await _generator.GenerateEnemiesAsync("dragons", 3);

        // Assert
        enemies.Should().AllSatisfy(enemy =>
        {
            enemy.Description.Should().NotBeNullOrEmpty();
        });
    }
}
