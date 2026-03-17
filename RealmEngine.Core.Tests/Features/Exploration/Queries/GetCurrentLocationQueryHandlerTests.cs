using FluentAssertions;
using Moq;
using RealmEngine.Core.Features.Exploration.Queries;
using RealmEngine.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealmEngine.Core.Tests.Features.Exploration.Queries;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for GetCurrentLocationQueryHandler.
/// </summary>
public class GetCurrentLocationQueryHandlerTests
{
    private readonly Mock<GameStateService> _mockGameState;
    private readonly GetCurrentLocationQueryHandler _handler;

    public GetCurrentLocationQueryHandlerTests()
    {
        _mockGameState = new Mock<GameStateService>();
        _handler = new GetCurrentLocationQueryHandler(_mockGameState.Object, NullLogger<GetCurrentLocationQueryHandler>.Instance);
    }

    [Fact]
    public async Task Handle_Should_Return_Current_Location()
    {
        // Arrange
        var currentLocation = "Mystic Forest";
        var query = new GetCurrentLocationQuery();

        _mockGameState.SetupGet(s => s.CurrentLocation).Returns(currentLocation);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.CurrentLocation.Should().Be(currentLocation);
        result.ErrorMessage.Should().BeNull();
        _mockGameState.VerifyGet(s => s.CurrentLocation, Times.Once);
    }

    [Theory]
    [InlineData("Starting Village")]
    [InlineData("Dark Dungeon")]
    [InlineData("Royal Castle")]
    [InlineData("Abandoned Mine")]
    [InlineData("Sky Temple")]
    public async Task Handle_Should_Return_Various_Locations(string location)
    {
        // Arrange
        var query = new GetCurrentLocationQuery();

        _mockGameState.SetupGet(s => s.CurrentLocation).Returns(location);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.CurrentLocation.Should().Be(location);
    }

    [Fact]
    public async Task Handle_Should_Return_Empty_String_When_No_Location()
    {
        // Arrange
        var query = new GetCurrentLocationQuery();

        _mockGameState.SetupGet(s => s.CurrentLocation).Returns(string.Empty);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.CurrentLocation.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Handle_Exception_Gracefully()
    {
        // Arrange
        var query = new GetCurrentLocationQuery();
        var expectedException = new InvalidOperationException("GameState not initialized");

        _mockGameState.SetupGet(s => s.CurrentLocation).Throws(expectedException);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.CurrentLocation.Should().BeNull();
        result.ErrorMessage.Should().Be(expectedException.Message);
    }

    [Fact]
    public async Task Handle_Should_Not_Modify_GameState()
    {
        // Arrange
        var currentLocation = "Test Location";
        var query = new GetCurrentLocationQuery();

        _mockGameState.SetupGet(s => s.CurrentLocation).Returns(currentLocation);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert - Verify only get, no set
        _mockGameState.VerifyGet(s => s.CurrentLocation, Times.Once);
        _mockGameState.VerifyNoOtherCalls();
    }
}
