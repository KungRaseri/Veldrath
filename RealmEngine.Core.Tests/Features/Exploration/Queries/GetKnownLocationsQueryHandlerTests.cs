using FluentAssertions;
using Moq;
using RealmEngine.Core.Features.Exploration;
using RealmEngine.Core.Features.Exploration.Queries;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealmEngine.Core.Tests.Features.Exploration.Queries;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for GetKnownLocationsQueryHandler.
/// </summary>
public class GetKnownLocationsQueryHandlerTests
{
    private readonly Mock<ExplorationService> _mockExplorationService;
    private readonly GetKnownLocationsQueryHandler _handler;

    public GetKnownLocationsQueryHandlerTests()
    {
        _mockExplorationService = new Mock<ExplorationService>();
        _handler = new GetKnownLocationsQueryHandler(_mockExplorationService.Object, NullLogger<GetKnownLocationsQueryHandler>.Instance);
    }

    [Fact]
    public async Task Handle_Should_Return_Known_Locations()
    {
        // Arrange
        var knownLocations = new List<Location>
        {
            new Location { Id = "towns:starting-village", Name = "Starting Village", Description = "A peaceful starting village", Type = "towns" },
            new Location { Id = "wilderness:dark-forest", Name = "Dark Forest", Description = "A dark and mysterious forest", Type = "wilderness" },
            new Location { Id = "dungeons:ancient-ruins", Name = "Ancient Ruins", Description = "Ancient ruins filled with secrets", Type = "dungeons" },
            new Location { Id = "wilderness:mountain-peak", Name = "Mountain Peak", Description = "A towering mountain peak", Type = "wilderness" }
        };
        var query = new GetKnownLocationsQuery();

        _mockExplorationService
            .Setup(s => s.GetKnownLocationsAsync())
            .ReturnsAsync(knownLocations);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Locations.Should().NotBeNull();
        result.Locations.Should().BeEquivalentTo(knownLocations.Select(l => l.Name));
        result.ErrorMessage.Should().BeNull();
        _mockExplorationService.Verify(s => s.GetKnownLocationsAsync(), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Empty_List_When_No_Locations_Known()
    {
        // Arrange
        var emptyList = new List<Location>();
        var query = new GetKnownLocationsQuery();

        _mockExplorationService
            .Setup(s => s.GetKnownLocationsAsync())
            .ReturnsAsync(emptyList);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Locations.Should().NotBeNull();
        result.Locations.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Return_Single_Location()
    {
        // Arrange
        var singleLocation = new List<Location> { new Location { Id = "towns:starting-village", Name = "Starting Village", Description = "A peaceful starting village", Type = "towns" } };
        var query = new GetKnownLocationsQuery();

        _mockExplorationService
            .Setup(s => s.GetKnownLocationsAsync())
            .ReturnsAsync(singleLocation);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Locations.Should().ContainSingle();
        result.Locations!.First().Should().Be("Starting Village");
    }

    [Fact]
    public async Task Handle_Should_Preserve_Location_Order()
    {
        // Arrange
        var orderedLocations = new List<Location>
        {
            new Location { Id = "towns:first-location", Name = "First Location", Description = "First test location", Type = "towns" },
            new Location { Id = "dungeons:second-location", Name = "Second Location", Description = "Second test location", Type = "dungeons" },
            new Location { Id = "wilderness:third-location", Name = "Third Location", Description = "Third test location", Type = "wilderness" }
        };
        var query = new GetKnownLocationsQuery();

        _mockExplorationService
            .Setup(s => s.GetKnownLocationsAsync())
            .ReturnsAsync(orderedLocations);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Locations.Should().ContainInOrder(orderedLocations.Select(l => l.Name));
    }

    [Fact]
    public async Task Handle_Should_Return_Large_List_Of_Locations()
    {
        // Arrange
        var manyLocations = Enumerable.Range(1, 100)
            .Select(i => new Location 
            { 
                Id = $"towns:location-{i}",
                Name = $"Location {i}", 
                Description = $"Test location {i}",
                Type = "towns" 
            })
            .ToList();
        var query = new GetKnownLocationsQuery();

        _mockExplorationService
            .Setup(s => s.GetKnownLocationsAsync())
            .ReturnsAsync(manyLocations);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Locations.Should().HaveCount(100);
    }

    [Fact]
    public async Task Handle_Should_Handle_Exception_Gracefully()
    {
        // Arrange
        var query = new GetKnownLocationsQuery();
        var expectedException = new InvalidOperationException("Database error");

        _mockExplorationService
            .Setup(s => s.GetKnownLocationsAsync())
            .ThrowsAsync(expectedException);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Locations.Should().BeNull();
        result.ErrorMessage.Should().Be(expectedException.Message);
    }

    [Fact]
    public async Task Handle_Should_Return_Readonly_List()
    {
        // Arrange
        var locations = new List<Location> 
        { 
            new Location { Id = "towns:location-1", Name = "Location 1", Description = "First test location", Type = "towns" }, 
            new Location { Id = "dungeons:location-2", Name = "Location 2", Description = "Second test location", Type = "dungeons" } 
        };
        var query = new GetKnownLocationsQuery();

        _mockExplorationService
            .Setup(s => s.GetKnownLocationsAsync())
            .ReturnsAsync(locations);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Locations.Should().BeAssignableTo<IReadOnlyList<string>>();
    }

    [Fact]
    public async Task Handle_Should_Not_Modify_Exploration_Service_State()
    {
        // Arrange
        var locations = new List<Location> { new Location { Id = "towns:test-location", Name = "Test Location", Description = "A test location", Type = "towns" } };
        var query = new GetKnownLocationsQuery();

        _mockExplorationService
            .Setup(s => s.GetKnownLocationsAsync())
            .ReturnsAsync(locations);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert - Verify only read operation
        _mockExplorationService.Verify(s => s.GetKnownLocationsAsync(), Times.Once);
        _mockExplorationService.VerifyNoOtherCalls();
    }
}
