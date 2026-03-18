using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealmEngine.Core.Features.Exploration;
using RealmEngine.Core.Features.Exploration.Queries;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Models;
using Xunit;

namespace RealmEngine.Core.Tests.Features.Exploration.Queries;

[Trait("Category", "Feature")]
public class GetLocationDetailHandlerTests
{
    private readonly Mock<IGameStateService> _mockGameState;
    private readonly Mock<ExplorationService> _mockExplorationService;
    private readonly Mock<ILogger<GetLocationDetailHandler>> _mockLogger;
    private readonly GetLocationDetailHandler _handler;

    public GetLocationDetailHandlerTests()
    {
        _mockGameState = new Mock<IGameStateService>();
        _mockExplorationService = new Mock<ExplorationService>();
        _mockLogger = new Mock<ILogger<GetLocationDetailHandler>>();
        
        _handler = new GetLocationDetailHandler(
            _mockGameState.Object,
            _mockExplorationService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Should_Return_Location_Detail_For_Current_Location()
    {
        // Arrange
        var currentLocation = "Dark Forest";
        _mockGameState.Setup(x => x.CurrentLocation).Returns(currentLocation);

        var location = new Location
        {
            Id = "darkforest",
            Name = currentLocation,
            Description = "A dangerous forest filled with monsters",
            Type = "wilderness",
            Level = 5,
            DangerRating = 7,
            Enemies = new List<string>
            {
                "@enemies/beasts/wolves:Wolf",
                "@enemies/beasts/wolves:Dire Wolf",
                "@enemies/undead/zombies:Zombie"
            },
            Loot = new List<string>
            {
                "@items/weapons/swords:*",
                "@items/consumables/potions:*"
            },
            Npcs = new List<string> { "hermit_npc" },
            Features = new List<string> { "save_point" }
        };

        _mockExplorationService
            .Setup(x => x.GetKnownLocationsAsync())
            .ReturnsAsync(new List<Location> { location }.AsReadOnly());

        // Act
        var result = await _handler.Handle(new GetLocationDetailQuery(), default);

        // Assert
        result.Success.Should().BeTrue();
        result.LocationName.Should().Be(currentLocation);
        result.LocationType.Should().Be("wilderness");
        result.RecommendedLevel.Should().Be(5);
        result.DangerRating.Should().Be(7);
        result.Description.Should().Be("A dangerous forest filled with monsters");
    }

    [Fact]
    public async Task Should_Return_Location_Detail_For_Specified_Location()
    {
        // Arrange
        var requestedLocation = "Town Square";
        var location = new Location
        {
            Id = "townsquare",
            Name = requestedLocation,
            Description = "A bustling town center",
            Type = "town",
            Level = 1,
            DangerRating = 0,
            Enemies = new List<string>(),
            Loot = new List<string>(),
            Npcs = new List<string> { "merchant_npc", "guard_npc" }
        };

        _mockExplorationService
            .Setup(x => x.GetKnownLocationsAsync())
            .ReturnsAsync(new List<Location> { location }.AsReadOnly());

        // Act
        var result = await _handler.Handle(new GetLocationDetailQuery(requestedLocation), default);

        // Assert
        result.Success.Should().BeTrue();
        result.LocationName.Should().Be(requestedLocation);
        result.LocationType.Should().Be("town");
        result.RecommendedLevel.Should().Be(1);
        result.DangerRating.Should().Be(0);
    }

    [Fact]
    public async Task Should_Calculate_Enemy_Spawn_Weights_By_Category()
    {
        // Arrange
        _mockGameState.Setup(x => x.CurrentLocation).Returns("Monster Den");

        var location = new Location
        {
            Id = "monsterden",
            Name = "Monster Den",
            Description = "A den of monsters",
            Type = "dungeon",
            Level = 10,
            DangerRating = 8,
            Enemies = new List<string>
            {
                "@enemies/beasts/wolves:Wolf",
                "@enemies/beasts/wolves:Dire Wolf",
                "@enemies/beasts/wolves:Alpha Wolf",
                "@enemies/demons/imps:Imp",
                "@enemies/undead/zombies:Zombie"
            },
            Loot = new List<string>()
        };

        _mockExplorationService
            .Setup(x => x.GetKnownLocationsAsync())
            .ReturnsAsync(new List<Location> { location }.AsReadOnly());

        // Act
        var result = await _handler.Handle(new GetLocationDetailQuery(), default);

        // Assert
        result.Success.Should().BeTrue();
        result.EnemySpawnWeights.Should().HaveCount(3);
        result.EnemySpawnWeights["beasts/wolves"].Should().Be(30); // 3 wolf variants = 30 weight
        result.EnemySpawnWeights["demons/imps"].Should().Be(10); // 1 imp = 10 weight
        result.EnemySpawnWeights["undead/zombies"].Should().Be(10); // 1 zombie = 10 weight
    }

    [Fact]
    public async Task Should_Calculate_Loot_Spawn_Weights_By_Category()
    {
        // Arrange
        _mockGameState.Setup(x => x.CurrentLocation).Returns("Treasure Cave");

        var location = new Location
        {
            Id = "treasurecave",
            Name = "Treasure Cave",
            Description = "A cave full of treasure",
            Type = "dungeon",
            Level = 8,
            DangerRating = 6,
            Enemies = new List<string>(),
            Loot = new List<string>
            {
                "@items/weapons/swords:*",
                "@items/weapons/swords:Iron Sword",
                "@items/armor/helmets:*",
                "@items/consumables/potions:Health Potion"
            }
        };

        _mockExplorationService
            .Setup(x => x.GetKnownLocationsAsync())
            .ReturnsAsync(new List<Location> { location }.AsReadOnly());

        // Act
        var result = await _handler.Handle(new GetLocationDetailQuery(), default);

        // Assert
        result.Success.Should().BeTrue();
        result.LootSpawnWeights.Should().HaveCount(3);
        result.LootSpawnWeights["weapons/swords"].Should().Be(20); // 2 sword references = 20 weight
        result.LootSpawnWeights["armor/helmets"].Should().Be(10); // 1 helmet reference = 10 weight
        result.LootSpawnWeights["consumables/potions"].Should().Be(10); // 1 potion reference = 10 weight
    }

    [Fact]
    public async Task Should_Return_Error_When_Location_Not_Found()
    {
        // Arrange
        var nonExistentLocation = "Atlantis";
        _mockExplorationService
            .Setup(x => x.GetKnownLocationsAsync())
            .ReturnsAsync(new List<Location>().AsReadOnly());

        // Act
        var result = await _handler.Handle(new GetLocationDetailQuery(nonExistentLocation), default);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Should_Return_Error_When_No_Location_Specified_And_No_Current_Location()
    {
        // Arrange
        _mockGameState.Setup(x => x.CurrentLocation).Returns((string)null!);

        // Act
        var result = await _handler.Handle(new GetLocationDetailQuery(), default);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No location specified");
    }

    [Fact]
    public async Task Should_Include_All_Enemy_References()
    {
        // Arrange
        _mockGameState.Setup(x => x.CurrentLocation).Returns("Haunted Castle");

        var location = new Location
        {
            Id = "hauntedcastle",
            Name = "Haunted Castle",
            Description = "A spooky castle",
            Type = "dungeon",
            Level = 15,
            DangerRating = 9,
            Enemies = new List<string>
            {
                "@enemies/undead/zombies:Zombie",
                "@enemies/undead/skeletons:Skeleton",
                "@enemies/undead/ghosts:Ghost"
            },
            Loot = new List<string>()
        };

        _mockExplorationService
            .Setup(x => x.GetKnownLocationsAsync())
            .ReturnsAsync(new List<Location> { location }.AsReadOnly());

        // Act
        var result = await _handler.Handle(new GetLocationDetailQuery(), default);

        // Assert
        result.Success.Should().BeTrue();
        result.EnemyReferences.Should().HaveCount(3);
        result.EnemyReferences.Should().Contain("@enemies/undead/zombies:Zombie");
        result.EnemyReferences.Should().Contain("@enemies/undead/skeletons:Skeleton");
        result.EnemyReferences.Should().Contain("@enemies/undead/ghosts:Ghost");
    }

    [Fact]
    public async Task Should_Include_All_Loot_References()
    {
        // Arrange
        _mockGameState.Setup(x => x.CurrentLocation).Returns("Ancient Ruins");

        var location = new Location
        {
            Id = "ancientruins",
            Name = "Ancient Ruins",
            Description = "Ruins of an ancient civilization",
            Type = "dungeon",
            Level = 12,
            DangerRating = 7,
            Enemies = new List<string>(),
            Loot = new List<string>
            {
                "@items/weapons:*",
                "@items/armor:*",
                "@items/accessories:*"
            }
        };

        _mockExplorationService
            .Setup(x => x.GetKnownLocationsAsync())
            .ReturnsAsync(new List<Location> { location }.AsReadOnly());

        // Act
        var result = await _handler.Handle(new GetLocationDetailQuery(), default);

        // Assert
        result.Success.Should().BeTrue();
        result.LootReferences.Should().HaveCount(3);
        result.LootReferences.Should().Contain("@items/weapons:*");
        result.LootReferences.Should().Contain("@items/armor:*");
        result.LootReferences.Should().Contain("@items/accessories:*");
    }

    [Fact]
    public async Task Should_Separate_NPCs_And_Merchants()
    {
        // Arrange
        _mockGameState.Setup(x => x.CurrentLocation).Returns("Trading Post");

        var location = new Location
        {
            Id = "tradingpost",
            Name = "Trading Post",
            Description = "A busy trading hub",
            Type = "town",
            Level = 1,
            DangerRating = 0,
            Enemies = new List<string>(),
            Loot = new List<string>(),
            Npcs = new List<string>
            {
                "blacksmith_merchant",
                "potion_merchant",
                "guard_npc",
                "traveler_npc"
            }
        };

        _mockExplorationService
            .Setup(x => x.GetKnownLocationsAsync())
            .ReturnsAsync(new List<Location> { location }.AsReadOnly());

        // Act
        var result = await _handler.Handle(new GetLocationDetailQuery(), default);

        // Assert
        result.Success.Should().BeTrue();
        result.AvailableMerchants.Should().HaveCount(2);
        result.AvailableMerchants.Should().Contain("blacksmith_merchant");
        result.AvailableMerchants.Should().Contain("potion_merchant");
        result.AvailableNPCs.Should().HaveCount(2);
        result.AvailableNPCs.Should().Contain("guard_npc");
        result.AvailableNPCs.Should().Contain("traveler_npc");
    }

    [Fact]
    public async Task Should_Include_Location_Features()
    {
        // Arrange
        _mockGameState.Setup(x => x.CurrentLocation).Returns("Safe Haven");

        var location = new Location
        {
            Id = "safehaven",
            Name = "Safe Haven",
            Description = "A safe rest area",
            Type = "town",
            Level = 1,
            DangerRating = 0,
            Enemies = new List<string>(),
            Loot = new List<string>(),
            Features = new List<string> { "save_point", "fast_travel", "training_area", "rest_point" }
        };

        _mockExplorationService
            .Setup(x => x.GetKnownLocationsAsync())
            .ReturnsAsync(new List<Location> { location }.AsReadOnly());

        // Act
        var result = await _handler.Handle(new GetLocationDetailQuery(), default);

        // Assert
        result.Success.Should().BeTrue();
        result.Features.Should().HaveCount(4);
        result.Features.Should().Contain("save_point");
        result.Features.Should().Contain("fast_travel");
        result.Features.Should().Contain("training_area");
        result.Features.Should().Contain("rest_point");
    }

    [Fact]
    public async Task Should_Handle_Empty_Collections_For_Safe_Location()
    {
        // Arrange
        _mockGameState.Setup(x => x.CurrentLocation).Returns("Peaceful Village");

        var location = new Location
        {
            Id = "peacefulvillage",
            Name = "Peaceful Village",
            Description = "A peaceful village",
            Type = "town",
            Level = 1,
            DangerRating = 0,
            Enemies = new List<string>(), // No enemies
            Loot = new List<string>(), // No loot
            Npcs = new List<string>()  // No NPCs
        };

        _mockExplorationService
            .Setup(x => x.GetKnownLocationsAsync())
            .ReturnsAsync(new List<Location> { location }.AsReadOnly());

        // Act
        var result = await _handler.Handle(new GetLocationDetailQuery(), default);

        // Assert
        result.Success.Should().BeTrue();
        result.EnemySpawnWeights.Should().BeEmpty();
        result.EnemyReferences.Should().BeEmpty();
        result.LootSpawnWeights.Should().BeEmpty();
        result.LootReferences.Should().BeEmpty();
        result.AvailableNPCs.Should().BeEmpty();
        result.AvailableMerchants.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_Include_Parent_Region_If_Present()
    {
        // Arrange
        _mockGameState.Setup(x => x.CurrentLocation).Returns("Darkwood Cave");

        var location = new Location
        {
            Id = "darkwoodcave",
            Name = "Darkwood Cave",
            Description = "A cave in Darkwood Forest",
            Type = "dungeon",
            Level = 6,
            DangerRating = 5,
            ParentRegion = "Darkwood Forest",
            Enemies = new List<string>(),
            Loot = new List<string>()
        };

        _mockExplorationService
            .Setup(x => x.GetKnownLocationsAsync())
            .ReturnsAsync(new List<Location> { location }.AsReadOnly());

        // Act
        var result = await _handler.Handle(new GetLocationDetailQuery(), default);

        // Assert
        result.Success.Should().BeTrue();
        result.ParentRegion.Should().Be("Darkwood Forest");
    }

    [Fact]
    public async Task Should_Include_Metadata()
    {
        // Arrange
        _mockGameState.Setup(x => x.CurrentLocation).Returns("Volcanic Peak");

        var location = new Location
        {
            Id = "volcanicpeak",
            Name = "Volcanic Peak",
            Description = "A dangerous volcanic mountain",
            Type = "wilderness",
            Level = 20,
            DangerRating = 10,
            Enemies = new List<string>(),
            Loot = new List<string>(),
            Metadata = new Dictionary<string, object>
            {
                { "terrain", "volcanic" },
                { "climate", "hot" },
                { "hazards", new List<string> { "lava", "falling_rocks" } }
            }
        };

        _mockExplorationService
            .Setup(x => x.GetKnownLocationsAsync())
            .ReturnsAsync(new List<Location> { location }.AsReadOnly());

        // Act
        var result = await _handler.Handle(new GetLocationDetailQuery(), default);

        // Assert
        result.Success.Should().BeTrue();
        result.Metadata.Should().ContainKey("terrain");
        result.Metadata["terrain"].Should().Be("volcanic");
        result.Metadata.Should().ContainKey("climate");
        result.Metadata["climate"].Should().Be("hot");
    }
}
