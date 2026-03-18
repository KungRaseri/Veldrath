using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealmEngine.Core.Features.Exploration;
using RealmEngine.Core.Features.Exploration.Queries;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Services;
using RealmEngine.Data.Services;
using RealmEngine.Shared.Models;
using Xunit;

namespace RealmEngine.Core.Tests.Features.Exploration.Queries;

[Trait("Category", "Feature")]
public class GetLocationSpawnInfoHandlerTests
{
    private readonly Mock<IGameStateService> _mockGameState;
    private readonly Mock<ExplorationService> _mockExplorationService;
    private readonly Mock<ILogger<GetLocationSpawnInfoHandler>> _mockLogger;
    private readonly GetLocationSpawnInfoHandler _handler;

    public GetLocationSpawnInfoHandlerTests()
    {
        _mockGameState = new Mock<IGameStateService>();
        _mockExplorationService = new Mock<ExplorationService>();
        _mockLogger = new Mock<ILogger<GetLocationSpawnInfoHandler>>();
        
        _handler = new GetLocationSpawnInfoHandler(
            _mockGameState.Object,
            _mockExplorationService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_Should_Return_Spawn_Info_For_Current_Location()
    {
        // Arrange
        var testLocation = new Location
        {
            Id = "dungeons:test-dungeon",
            Name = "Test Dungeon",
            Description = "A dangerous dungeon",
            Type = "dungeon",
            DangerRating = 75,
            Enemies = new List<string>
            {
                "@enemies/undead/zombies:Zombie",
                "@enemies/undead/skeletons:Skeleton"
            },
            Loot = new List<string>
            {
                "@items/weapons:*",
                "@items/armor:*"
            },
            Metadata = new Dictionary<string, object>
            {
                { "recommendedLevel", "5-10" }
            }
        };

        _mockGameState.Setup(g => g.CurrentLocation).Returns("Test Dungeon");
        _mockExplorationService.Setup(e => e.GetKnownLocationsAsync())
            .ReturnsAsync(new List<Location> { testLocation });

        var query = new GetLocationSpawnInfoQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.LocationName.Should().Be("Test Dungeon");
        result.LocationType.Should().Be("dungeon");
        result.DangerRating.Should().Be(75);
        result.RecommendedLevel.Should().Be("5-10");
        result.EnemyReferences.Should().HaveCount(2);
        result.EnemyReferences.Should().Contain("@enemies/undead/zombies:Zombie");
        result.EnemySpawnWeights.Should().ContainKey("undead/zombies");
        result.EnemySpawnWeights.Should().ContainKey("undead/skeletons");
        result.LootReferences.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_Should_Return_Spawn_Info_For_Specified_Location()
    {
        // Arrange
        var dungeonLocation = new Location
        {
            Id = "dungeons:crypt",
            Name = "Ancient Crypt",
            Description = "A dark crypt",
            Type = "dungeon",
            DangerRating = 80,
            Enemies = new List<string> { "@enemies/undead/zombies:*" }
        };

        var townLocation = new Location
        {
            Id = "towns:village",
            Name = "Peaceful Village",
            Description = "A safe town",
            Type = "town",
            DangerRating = 5,
            Enemies = new List<string>()
        };

        _mockExplorationService.Setup(e => e.GetKnownLocationsAsync())
            .ReturnsAsync(new List<Location> { dungeonLocation, townLocation });

        var query = new GetLocationSpawnInfoQuery("Peaceful Village");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.LocationName.Should().Be("Peaceful Village");
        result.DangerRating.Should().Be(5);
        result.EnemyReferences.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Aggregate_Enemy_Spawn_Weights_By_Category()
    {
        // Arrange
        var forestLocation = new Location
        {
            Id = "wilderness:dark-forest",
            Name = "Dark Forest",
            Description = "Dense woods",
            Type = "wilderness",
            DangerRating = 60,
            Enemies = new List<string>
            {
                "@enemies/beasts/wolves:Wolf",
                "@enemies/beasts/wolves:Dire Wolf",
                "@enemies/beasts/wolves:Alpha Wolf",
                "@enemies/goblinoids/goblins:Goblin",
                "@enemies/goblinoids/goblins:Goblin Scout"
            }
        };

        _mockGameState.Setup(g => g.CurrentLocation).Returns("Dark Forest");
        _mockExplorationService.Setup(e => e.GetKnownLocationsAsync())
            .ReturnsAsync(new List<Location> { forestLocation });

        var query = new GetLocationSpawnInfoQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.EnemySpawnWeights.Should().ContainKey("beasts/wolves");
        result.EnemySpawnWeights["beasts/wolves"].Should().Be(30); // 3 wolf variants * 10
        result.EnemySpawnWeights.Should().ContainKey("goblinoids/goblins");
        result.EnemySpawnWeights["goblinoids/goblins"].Should().Be(20); // 2 goblin variants * 10
    }

    [Fact]
    public async Task Handle_Should_Return_Error_When_Location_Not_Found()
    {
        // Arrange
        _mockExplorationService.Setup(e => e.GetKnownLocationsAsync())
            .ReturnsAsync(new List<Location>());

        var query = new GetLocationSpawnInfoQuery("Nonexistent Location");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
        result.LocationName.Should().Be("Nonexistent Location");
    }

    [Fact]
    public async Task Handle_Should_Return_Error_When_No_Location_Specified()
    {
        // Arrange
        _mockGameState.Setup(g => g.CurrentLocation).Returns((string)null!);
        var query = new GetLocationSpawnInfoQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No location specified");
    }

    [Fact]
    public async Task Handle_Should_Extract_Loot_References_From_Location()
    {
        // Arrange
        var location = new Location
        {
            Id = "dungeons:treasure-vault",
            Name = "Treasure Vault",
            Description = "Rich with loot",
            Type = "dungeon",
            DangerRating = 90,
            Loot = new List<string>
            {
                "@items/weapons/swords:*",
                "@items/armor/heavy:*",
                "@items/consumables/potions:*",
                "@items/materials/gems:Diamond"
            }
        };

        _mockGameState.Setup(g => g.CurrentLocation).Returns("Treasure Vault");
        _mockExplorationService.Setup(e => e.GetKnownLocationsAsync())
            .ReturnsAsync(new List<Location> { location });

        var query = new GetLocationSpawnInfoQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.LootReferences.Should().HaveCount(4);
        result.LootReferences.Should().Contain("@items/weapons/swords:*");
        result.LootReferences.Should().Contain("@items/materials/gems:Diamond");
    }

    [Fact]
    public async Task Handle_Should_Return_Empty_Collections_For_Safe_Location()
    {
        // Arrange
        var safeLocation = new Location
        {
            Id = "towns:capital",
            Name = "Capital City",
            Description = "The main city",
            Type = "town",
            DangerRating = 0,
            Enemies = new List<string>(),
            Loot = new List<string>(),
            Npcs = new List<string> { "blacksmith", "merchant", "innkeeper" }
        };

        _mockGameState.Setup(g => g.CurrentLocation).Returns("Capital City");
        _mockExplorationService.Setup(e => e.GetKnownLocationsAsync())
            .ReturnsAsync(new List<Location> { safeLocation });

        var query = new GetLocationSpawnInfoQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.DangerRating.Should().Be(0);
        result.EnemyReferences.Should().BeEmpty();
        result.EnemySpawnWeights.Should().BeEmpty();
        result.LootReferences.Should().BeEmpty();
        result.AvailableNPCs.Should().HaveCount(3);
    }
}
