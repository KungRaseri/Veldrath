using RealmEngine.Core.Features.Exploration;
using RealmEngine.Core.Services;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.Death.Queries;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Core.Generators;
using RealmEngine.Shared.Models;
using MediatR;
using Moq;
using FluentAssertions;
using Xunit;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Tests.Services;

[Trait("Category", "Service")]
public class ExplorationServiceTests
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<GameStateService> _mockGameState;
    private readonly Mock<SaveGameService> _mockSaveGameService;
    private readonly StubLocationGenerator _locationGenerator;
    private readonly ExplorationService _service;
    
    private class StubLocationGenerator : LocationGenerator
    {
        private readonly List<Location> _testLocations;
        
        public StubLocationGenerator() : base()
        {
            _testLocations = new List<Location>
            {
                new Location { Id = "towns:test-town", Name = "Test Town", Description = "A test town location", Type = "towns" },
                new Location { Id = "dungeons:test-dungeon", Name = "Test Dungeon", Description = "A test dungeon location", Type = "dungeons" }
            };
        }
        
        public override Task<List<Location>> GenerateLocationsAsync(string locationType, int count = 5, bool hydrate = true)
        {
            return Task.FromResult(_testLocations);
        }
    }

    public ExplorationServiceTests()
    {
        _mockMediator = new Mock<IMediator>();
        _mockGameState = new Mock<GameStateService>();
        _mockSaveGameService = new Mock<SaveGameService>();
        _locationGenerator = new StubLocationGenerator();

        var testPlayer = new Character
        {
            Name = "TestHero",
            Level = 5,
            Experience = 0,
            Gold = 100,
            MaxHealth = 100,
            Health = 100
        };
        _mockGameState.Setup(g => g.Player).Returns(testPlayer);
        _mockGameState.Setup(g => g.CurrentLocation).Returns("Test Town");
        _mockSaveGameService.Setup(s => s.GetCurrentSave()).Returns(new SaveGame
        {
            Character = testPlayer,
            DroppedItemsAtLocations = new Dictionary<string, List<Item>>()
        });

        // Mock GetDroppedItemsQuery to return empty list
        _mockMediator.Setup(m => m.Send(It.IsAny<GetDroppedItemsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetDroppedItemsResult { Items = new List<Item>() });

        _service = new ExplorationService(
            _mockMediator.Object,
            _mockGameState.Object,
            _mockSaveGameService.Object,
            _locationGenerator,
            itemGenerator: null);
    }

    [Fact]
    public async Task ExploreAsync_Should_Return_Combat_Or_Peaceful()
    {
        var result = await _service.ExploreAsync();
        result.Success.Should().BeTrue();
        result.CurrentLocation.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetAvailableLocations_Should_Return_Locations()
    {
        var result = await _service.GetAvailableLocations();
        result.Success.Should().BeTrue();
        result.CurrentLocation.Should().Be("Test Town");
        result.AvailableLocations.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task TravelToLocation_Should_Update_Location()
    {
        var result = await _service.TravelToLocation("Test Dungeon");
        result.Success.Should().BeTrue();
        result.CurrentLocation.Should().Be("Test Dungeon");
    }
}
