using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Features.Exploration.Commands;
using RealmEngine.Core.Services;
using RealmEngine.Core.Tests.Infrastructure;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Exploration.Commands;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for ExploreLocationCommandHandler.
/// </summary>
public class ExploreLocationCommandHandlerTests
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IGameStateService> _mockGameState;
    private readonly ExploreLocationCommandHandler _handler;
    private readonly Character _player;

    public ExploreLocationCommandHandlerTests()
    {
        _mockMediator = new Mock<IMediator>();
        _mockGameState = new Mock<IGameStateService>();
        _player = new Character { Name = "Hero", Level = 1, Experience = 0, Gold = 0, Health = 100, MaxHealth = 100 };

        _mockGameState.SetupGet(s => s.Player).Returns(_player);
        _mockGameState.SetupGet(s => s.CurrentLocation).Returns("Forest");

        // Scripted peaceful exploration by default: every roll is deterministic.
        _handler = CreateHandler(PeacefulExploration());
    }

    private ExploreLocationCommandHandler CreateHandler(IRandomSource? randomSource = null) =>
        new(
            _mockMediator.Object,
            _mockGameState.Object,
            NullLogger<ExploreLocationCommandHandler>.Instance,
            randomSource: randomSource ?? FakeRandomSource.Seeded(20260101));

    /// <summary>
    /// The handler rolls, in order: encounter (0-99), XP (10-29), gold (5-24), item (0-99).
    /// </summary>
    private static FakeRandomSource PeacefulExploration(int xp = 20, int gold = 10) =>
        FakeRandomSource.Returning(70, xp, gold, 50);

    [Fact]
    public async Task Handle_Should_Trigger_Combat_Encounter_When_The_Encounter_Roll_Is_Low()
    {
        // Arrange — 0 is below the 60% combat threshold.
        var handler = CreateHandler(FakeRandomSource.Returning(0));

        // Act
        var result = await handler.Handle(new ExploreLocationCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.CombatTriggered.Should().BeTrue();
        result.ExperienceGained.Should().BeNull();
        result.GoldGained.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_Award_Experience_On_Peaceful_Exploration()
    {
        // Act
        var result = await _handler.Handle(new ExploreLocationCommand(), CancellationToken.None);

        // Assert
        result.CombatTriggered.Should().BeFalse();
        result.ExperienceGained.Should().BeGreaterThan(0);
        result.ExperienceGained.Should().BeInRange(10, 30);
        _player.Experience.Should().Be(result.ExperienceGained);
    }

    [Fact]
    public async Task Handle_Should_Award_Gold_On_Peaceful_Exploration()
    {
        // Act
        var result = await _handler.Handle(new ExploreLocationCommand(), CancellationToken.None);

        // Assert
        result.GoldGained.Should().BeGreaterThan(0);
        result.GoldGained.Should().BeInRange(5, 25);
        _player.Gold.Should().Be(result.GoldGained);
    }

    [Fact]
    public async Task Handle_Should_Treat_Roll_59_As_Combat_And_Roll_60_As_Peaceful()
    {
        // Arrange — the encounter threshold is exactly 60.
        var combatHandler = CreateHandler(FakeRandomSource.Returning(59));
        var peacefulHandler = CreateHandler(PeacefulExploration());

        // Act
        var combat = await combatHandler.Handle(new ExploreLocationCommand(), CancellationToken.None);
        var peaceful = await peacefulHandler.Handle(new ExploreLocationCommand(), CancellationToken.None);

        // Assert
        combat.CombatTriggered.Should().BeTrue();
        peaceful.CombatTriggered.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_Publish_Level_Up_Event_When_Player_Levels()
    {
        // Arrange — 95 XP + 20 from a peaceful exploration crosses the 100 XP threshold.
        _player.Experience = 95;
        var handler = CreateHandler(PeacefulExploration(xp: 20));

        // Act
        await handler.Handle(new ExploreLocationCommand(), CancellationToken.None);

        // Assert
        _player.Level.Should().BeGreaterThan(1, "peaceful exploration always triggers a level-up from 95 XP");
        _mockMediator.Verify(m => m.Publish(
            It.IsAny<INotification>(),
            It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Handle_Should_Publish_Gold_Gained_Event()
    {
        // Act
        var result = await _handler.Handle(new ExploreLocationCommand(), CancellationToken.None);

        // Assert - GoldGained event should be published on peaceful exploration
        result.CombatTriggered.Should().BeFalse();
        _mockMediator.Verify(m => m.Publish(
            It.IsAny<INotification>(),
            It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Handle_Should_Handle_Exception_Gracefully()
    {
        // Arrange
        _mockGameState.SetupGet(s => s.Player).Throws(new InvalidOperationException("Player not found"));

        // Act
        var result = await _handler.Handle(new ExploreLocationCommand(), CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }
}
