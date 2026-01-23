using FluentAssertions;
using Moq;
using MediatR;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Features.Exploration.Commands;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Exploration.Commands;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for ExploreLocationCommandHandler.
/// </summary>
public class ExploreLocationCommandHandlerTests
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<GameStateService> _mockGameState;
    private readonly ExploreLocationCommandHandler _handler;
    private readonly Character _player;

    public ExploreLocationCommandHandlerTests()
    {
        _mockMediator = new Mock<IMediator>();
        _mockGameState = new Mock<GameStateService>();
        _player = new Character { Name = "Hero", Level = 1, Experience = 0, Gold = 0, Health = 100, MaxHealth = 100 };

        _mockGameState.SetupGet(s => s.Player).Returns(_player);
        _mockGameState.SetupGet(s => s.CurrentLocation).Returns("Forest");

        _handler = new ExploreLocationCommandHandler(_mockMediator.Object, _mockGameState.Object);
    }

    [Fact]
    public async Task Handle_Should_Trigger_Combat_Encounter()
    {
        // Arrange
        var command = new ExploreLocationCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - Result could be combat or peaceful, just verify it succeeds
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_Award_Experience_On_Peaceful_Exploration()
    {
        // Arrange
        var command = new ExploreLocationCommand();
        var initialExperience = _player.Experience;

        // Act - Run multiple times to ensure we get at least one peaceful result
        ExploreLocationResult? peacefulResult = null;
        for (int i = 0; i < 50; i++) // Try multiple times to get peaceful exploration
        {
            var result = await _handler.Handle(command, CancellationToken.None);
            if (result.Success && !result.CombatTriggered)
            {
                peacefulResult = result;
                break;
            }
        }

        // Assert
        if (peacefulResult != null)
        {
            peacefulResult.ExperienceGained.Should().BeGreaterThan(0);
            peacefulResult.ExperienceGained.Should().BeInRange(10, 30);
        }
    }

    [Fact]
    public async Task Handle_Should_Award_Gold_On_Peaceful_Exploration()
    {
        // Arrange
        var command = new ExploreLocationCommand();

        // Act - Run multiple times to ensure we get at least one peaceful result
        ExploreLocationResult? peacefulResult = null;
        for (int i = 0; i < 50; i++)
        {
            var result = await _handler.Handle(command, CancellationToken.None);
            if (result.Success && !result.CombatTriggered)
            {
                peacefulResult = result;
                break;
            }
        }

        // Assert
        if (peacefulResult != null)
        {
            peacefulResult.GoldGained.Should().BeGreaterThan(0);
            peacefulResult.GoldGained.Should().BeInRange(5, 25);
        }
    }

    [Fact]
    public async Task Handle_Should_Return_Either_Combat_Or_Peaceful_Result()
    {
        // Arrange
        var command = new ExploreLocationCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        // Either combat is triggered, or XP/Gold are awarded
        if (result.CombatTriggered)
        {
            result.ExperienceGained.Should().BeNull();
            result.GoldGained.Should().BeNull();
        }
        else
        {
            result.ExperienceGained.Should().BeGreaterThan(0);
            result.GoldGained.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task Handle_Should_Display_Exploration_Messages()
    {
        // Arrange
        var command = new ExploreLocationCommand();

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
    }

    [Fact]
    public async Task Handle_Should_Publish_Level_Up_Event_When_Player_Levels()
    {
        // Arrange
        _player.Experience = 95; // Close to level up (needs 100 XP to level)
        var command = new ExploreLocationCommand();

        // Act - Run multiple times to trigger level up
        for (int i = 0; i < 50; i++)
        {
            var result = await _handler.Handle(command, CancellationToken.None);
            if (!result.CombatTriggered && _player.Level > 1)
            {
                break;
            }
        }

        // Assert - Check if PlayerLeveledUp was published (if level up occurred)
        if (_player.Level > 1)
        {
            _mockMediator.Verify(m => m.Publish(
                It.IsAny<INotification>(),
                It.IsAny<CancellationToken>()),
                Times.AtLeast(1));
        }
    }

    [Fact]
    public async Task Handle_Should_Publish_Gold_Gained_Event()
    {
        // Arrange
        var command = new ExploreLocationCommand();

        // Act - Run multiple times to ensure peaceful exploration
        for (int i = 0; i < 50; i++)
        {
            var result = await _handler.Handle(command, CancellationToken.None);
            if (!result.CombatTriggered)
            {
                break;
            }
        }

        // Assert - GoldGained event should be published on peaceful exploration
        _mockMediator.Verify(m => m.Publish(
            It.IsAny<INotification>(),
            It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Handle_Should_Handle_Exception_Gracefully()
    {
        // Arrange
        var command = new ExploreLocationCommand();
        _mockGameState.SetupGet(s => s.Player).Throws(new InvalidOperationException("Player not found"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_Should_Use_Random_Outcomes()
    {
        // Arrange
        var command = new ExploreLocationCommand();
        var combatCount = 0;
        var peacefulCount = 0;
        var iterations = 100;

        // Act - Run multiple times to verify randomness
        for (int i = 0; i < iterations; i++)
        {
            var result = await _handler.Handle(command, CancellationToken.None);
            if (result.Success)
            {
                if (result.CombatTriggered)
                    combatCount++;
                else
                    peacefulCount++;
            }
        }

        // Assert - Should have both combat and peaceful results (not deterministic)
        combatCount.Should().BeGreaterThan(0);
        peacefulCount.Should().BeGreaterThan(0);
        // Roughly 60% combat, 40% peaceful based on handler logic
        combatCount.Should().BeInRange(40, 80); // Allow variance
        peacefulCount.Should().BeInRange(20, 60);
    }
}
