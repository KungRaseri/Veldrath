using FluentAssertions;
using Moq;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Features.Exploration.Commands;
using RealmEngine.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealmEngine.Core.Tests.Features.Exploration.Commands;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for TravelToLocationCommandHandler.
/// </summary>
public class TravelToLocationCommandHandlerTests
{
    private readonly Mock<GameStateService> _mockGameState;
    private readonly TravelToLocationCommandHandler _handler;

    public TravelToLocationCommandHandlerTests()
    {
        _mockGameState = new Mock<GameStateService>();
        _mockGameState.SetupAllProperties();  // Enable automatic property tracking
        _mockGameState.Object.CurrentLocation = "Starting Village";
        
        // Mock UpdateLocation to properly update the property
        _mockGameState.Setup(s => s.UpdateLocation(It.IsAny<string>()))
            .Callback<string>(location => _mockGameState.Object.CurrentLocation = location);
        
        _handler = new TravelToLocationCommandHandler(_mockGameState.Object, NullLogger<TravelToLocationCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_Should_Travel_To_New_Location_Successfully()
    {
        // Arrange
        var destination = "Forest of Shadows";
        var command = new TravelToLocationCommand(destination);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.NewLocation.Should().Be(destination);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_Fail_When_Destination_Is_Empty()
    {
        // Arrange
        var command = new TravelToLocationCommand(string.Empty);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Destination cannot be empty");
        result.NewLocation.Should().BeNull();
        _mockGameState.Verify(s => s.UpdateLocation(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Fail_When_Destination_Is_Whitespace()
    {
        // Arrange
        var command = new TravelToLocationCommand("   ");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Destination cannot be empty");
        result.NewLocation.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_Fail_When_Already_At_Destination()
    {
        // Arrange
        var currentLocation = "Forest of Shadows";
        var command = new TravelToLocationCommand(currentLocation);

        _mockGameState.SetupGet(s => s.CurrentLocation).Returns(currentLocation);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Already at this location");
        result.NewLocation.Should().BeNull();
        _mockGameState.Verify(s => s.UpdateLocation(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Update_Location_In_GameState()
    {
        // Arrange
        var destination = "Dark Castle";
        var command = new TravelToLocationCommand(destination);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        _mockGameState.Object.CurrentLocation.Should().Be(destination);
    }

    [Fact]
    public async Task Handle_Should_Handle_Exception_Gracefully()
    {
        // Arrange
        var destination = "Invalid Location";
        var command = new TravelToLocationCommand(destination);
        var expectedException = new InvalidOperationException("Location not found");

        _mockGameState.SetupGet(s => s.CurrentLocation).Returns("Village");
        _mockGameState.Setup(s => s.UpdateLocation(destination)).Throws(expectedException);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be(expectedException.Message);
        result.NewLocation.Should().BeNull();
    }

    [Theory]
    [InlineData("Town Square")]
    [InlineData("Mystic Mountains")]
    [InlineData("Underground Cavern")]
    [InlineData("Sky Citadel")]
    public async Task Handle_Should_Travel_To_Various_Locations(string destination)
    {
        // Arrange
        var command = new TravelToLocationCommand(destination);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.NewLocation.Should().Be(destination);
    }
}
