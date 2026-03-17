using FluentAssertions;
using RealmEngine.Core.Features.Reputation.Services;
using RealmEngine.Shared.Models;
using Xunit;

namespace RealmEngine.Core.Tests.Features.Reputation;

/// <summary>
/// Tests for ReputationService.
/// </summary>
public class ReputationServiceTests
{
    private readonly ReputationService _service;

    public ReputationServiceTests()
    {
        _service = new ReputationService(NullLogger<ReputationService>.Instance);
    }

    [Fact]
    public void GetOrCreateReputation_ShouldCreateNewStanding_WhenFactionNotFound()
    {
        // Arrange
        var saveGame = new SaveGame { PlayerName = "TestHero" };
        var factionId = "merchants-guild";

        // Act
        var standing = _service.GetOrCreateReputation(saveGame, factionId);

        // Assert
        standing.Should().NotBeNull();
        standing.FactionId.Should().Be(factionId);
        standing.ReputationPoints.Should().Be(0);
        standing.Level.Should().Be(ReputationLevel.Neutral);
        saveGame.FactionReputations.Should().ContainKey(factionId);
    }

    [Fact]
    public void GetOrCreateReputation_ShouldReturnExisting_WhenFactionExists()
    {
        // Arrange
        var saveGame = new SaveGame { PlayerName = "TestHero" };
        var factionId = "merchants-guild";
        var existingStanding = new ReputationStanding 
        { 
            FactionId = factionId, 
            ReputationPoints = 1000 
        };
        saveGame.FactionReputations[factionId] = existingStanding;

        // Act
        var standing = _service.GetOrCreateReputation(saveGame, factionId);

        // Assert
        standing.Should().BeSameAs(existingStanding);
        standing.ReputationPoints.Should().Be(1000);
    }

    [Fact]
    public void GainReputation_ShouldIncreasePoints()
    {
        // Arrange
        var saveGame = new SaveGame { PlayerName = "TestHero" };
        var factionId = "merchants-guild";

        // Act
        var standing = _service.GainReputation(saveGame, factionId, 500);

        // Assert
        standing.ReputationPoints.Should().Be(500);
        standing.Level.Should().Be(ReputationLevel.Friendly);
    }

    [Fact]
    public void GainReputation_ShouldDetectLevelChange_WhenCrossingThreshold()
    {
        // Arrange
        var saveGame = new SaveGame { PlayerName = "TestHero" };
        var factionId = "merchants-guild";
        _service.GainReputation(saveGame, factionId, 400); // Neutral

        // Act
        var standing = _service.GainReputation(saveGame, factionId, 200); // Cross to Friendly at 500

        // Assert
        standing.ReputationPoints.Should().Be(600);
        standing.Level.Should().Be(ReputationLevel.Friendly);
    }

    [Fact]
    public void GainReputation_ShouldReachHonored_At3000Points()
    {
        // Arrange
        var saveGame = new SaveGame { PlayerName = "TestHero" };
        var factionId = "merchants-guild";

        // Act
        var standing = _service.GainReputation(saveGame, factionId, 3000);

        // Assert
        standing.ReputationPoints.Should().Be(3000);
        standing.Level.Should().Be(ReputationLevel.Honored);
    }

    [Fact]
    public void GainReputation_ShouldReachRevered_At6000Points()
    {
        // Arrange
        var saveGame = new SaveGame { PlayerName = "TestHero" };
        var factionId = "merchants-guild";

        // Act
        var standing = _service.GainReputation(saveGame, factionId, 6000);

        // Assert
        standing.ReputationPoints.Should().Be(6000);
        standing.Level.Should().Be(ReputationLevel.Revered);
    }

    [Fact]
    public void GainReputation_ShouldReachExalted_At12000Points()
    {
        // Arrange
        var saveGame = new SaveGame { PlayerName = "TestHero" };
        var factionId = "merchants-guild";

        // Act
        var standing = _service.GainReputation(saveGame, factionId, 12000);

        // Assert
        standing.ReputationPoints.Should().Be(12000);
        standing.Level.Should().Be(ReputationLevel.Exalted);
    }

    [Fact]
    public void LoseReputation_ShouldDecreasePoints()
    {
        // Arrange
        var saveGame = new SaveGame { PlayerName = "TestHero" };
        var factionId = "merchants-guild";
        _service.GainReputation(saveGame, factionId, 1000);

        // Act
        var standing = _service.LoseReputation(saveGame, factionId, 300);

        // Assert
        standing.ReputationPoints.Should().Be(700);
        standing.Level.Should().Be(ReputationLevel.Friendly);
    }

    [Fact]
    public void LoseReputation_ShouldDetectLevelChange_WhenCrossingThreshold()
    {
        // Arrange
        var saveGame = new SaveGame { PlayerName = "TestHero" };
        var factionId = "merchants-guild";
        _service.GainReputation(saveGame, factionId, 600); // Friendly

        // Act
        var standing = _service.LoseReputation(saveGame, factionId, 200); // Drop to Neutral

        // Assert
        standing.ReputationPoints.Should().Be(400);
        standing.Level.Should().Be(ReputationLevel.Neutral);
    }

    [Fact]
    public void LoseReputation_ShouldBecomeUnfriendly_AtMinus3000Points()
    {
        // Arrange
        var saveGame = new SaveGame { PlayerName = "TestHero" };
        var factionId = "merchants-guild";

        // Act
        var standing = _service.LoseReputation(saveGame, factionId, 3000);

        // Assert
        standing.ReputationPoints.Should().Be(-3000);
        standing.Level.Should().Be(ReputationLevel.Unfriendly);
    }

    [Fact]
    public void LoseReputation_ShouldBecomeHostile_AtMinus6000Points()
    {
        // Arrange
        var saveGame = new SaveGame { PlayerName = "TestHero" };
        var factionId = "merchants-guild";

        // Act
        var standing = _service.LoseReputation(saveGame, factionId, 6000);

        // Assert
        standing.ReputationPoints.Should().Be(-6000);
        standing.Level.Should().Be(ReputationLevel.Hostile);
    }

    [Fact]
    public void GetReputationLevel_ShouldReturnCorrectLevel_ForNeutralRange()
    {
        // Arrange
        var saveGame = new SaveGame { PlayerName = "TestHero" };
        var factionId = "merchants-guild";
        _service.GainReputation(saveGame, factionId, 100);

        // Act
        var standing = _service.GetOrCreateReputation(saveGame, factionId);

        // Assert
        standing.Level.Should().Be(ReputationLevel.Neutral);
    }

    [Fact]
    public void CheckReputationRequirement_ShouldReturnTrue_WhenRequirementMet()
    {
        // Arrange
        var saveGame = new SaveGame { PlayerName = "TestHero" };
        var factionId = "merchants-guild";
        _service.GainReputation(saveGame, factionId, 1000);

        // Act
        var canAccess = _service.CheckReputationRequirement(saveGame, factionId, ReputationLevel.Friendly);

        // Assert
        canAccess.Should().BeTrue();
    }

    [Fact]
    public void CheckReputationRequirement_ShouldReturnFalse_WhenRequirementNotMet()
    {
        // Arrange
        var saveGame = new SaveGame { PlayerName = "TestHero" };
        var factionId = "merchants-guild";
        _service.GainReputation(saveGame, factionId, 200); // Neutral

        // Act
        var canAccess = _service.CheckReputationRequirement(saveGame, factionId, ReputationLevel.Honored);

        // Assert
        canAccess.Should().BeFalse();
    }

    [Fact]
    public void CanTrade_ShouldReturnTrue_WhenNotHostile()
    {
        // Arrange
        var saveGame = new SaveGame { PlayerName = "TestHero" };
        var factionId = "merchants-guild";
        _service.GainReputation(saveGame, factionId, 100);

        // Act
        var canTrade = _service.CanTrade(saveGame, factionId);

        // Assert
        canTrade.Should().BeTrue();
    }

    [Fact]
    public void CanTrade_ShouldReturnFalse_WhenHostile()
    {
        // Arrange
        var saveGame = new SaveGame { PlayerName = "TestHero" };
        var factionId = "merchants-guild";
        _service.LoseReputation(saveGame, factionId, 7000); // Hostile

        // Act
        var canTrade = _service.CanTrade(saveGame, factionId);

        // Assert
        canTrade.Should().BeFalse();
    }

    [Fact]
    public void CanAcceptQuests_ShouldReturnTrue_WhenNotHostile()
    {
        // Arrange
        var saveGame = new SaveGame { PlayerName = "TestHero" };
        var factionId = "merchants-guild";
        _service.GainReputation(saveGame, factionId, 100);

        // Act
        var canQuest = _service.CanAcceptQuests(saveGame, factionId);

        // Assert
        canQuest.Should().BeTrue();
    }

    [Fact]
    public void CanAcceptQuests_ShouldReturnFalse_WhenHostile()
    {
        // Arrange
        var saveGame = new SaveGame { PlayerName = "TestHero" };
        var factionId = "merchants-guild";
        _service.LoseReputation(saveGame, factionId, 7000); // Hostile

        // Act
        var canQuest = _service.CanAcceptQuests(saveGame, factionId);

        // Assert
        canQuest.Should().BeFalse();
    }

    [Fact]
    public void IsHostile_ShouldReturnTrue_WhenHostile()
    {
        // Arrange
        var saveGame = new SaveGame { PlayerName = "TestHero" };
        var factionId = "merchants-guild";
        _service.LoseReputation(saveGame, factionId, 7000);

        // Act
        var isHostile = _service.IsHostile(saveGame, factionId);

        // Assert
        isHostile.Should().BeTrue();
    }

    [Fact]
    public void IsHostile_ShouldReturnFalse_WhenNotHostile()
    {
        // Arrange
        var saveGame = new SaveGame { PlayerName = "TestHero" };
        var factionId = "merchants-guild";
        _service.GainReputation(saveGame, factionId, 100);

        // Act
        var isHostile = _service.IsHostile(saveGame, factionId);

        // Assert
        isHostile.Should().BeFalse();
    }

    [Theory]
    [InlineData(ReputationLevel.Neutral, 0.0)]
    [InlineData(ReputationLevel.Friendly, 0.05)]
    [InlineData(ReputationLevel.Honored, 0.10)]
    [InlineData(ReputationLevel.Revered, 0.20)]
    [InlineData(ReputationLevel.Exalted, 0.30)]
    public void GetPriceDiscount_ShouldReturnCorrectDiscount(ReputationLevel level, double expectedDiscount)
    {
        // Arrange
        var saveGame = new SaveGame { PlayerName = "TestHero" };
        var factionId = "merchants-guild";
        
        // Set reputation to appropriate level
        int points = level switch
        {
            ReputationLevel.Friendly => 500,
            ReputationLevel.Honored => 3000,
            ReputationLevel.Revered => 6000,
            ReputationLevel.Exalted => 12000,
            _ => 0
        };
        
        if (points > 0)
        {
            _service.GainReputation(saveGame, factionId, points);
        }

        // Act
        var discount = _service.GetPriceDiscount(saveGame, factionId);

        // Assert
        discount.Should().BeApproximately(expectedDiscount, 0.001);
    }

    [Fact]
    public void GetPriceDiscount_ShouldReturnZero_WhenHostileOrUnfriendly()
    {
        // Arrange
        var saveGame = new SaveGame { PlayerName = "TestHero" };
        var factionId = "merchants-guild";
        _service.LoseReputation(saveGame, factionId, 4000); // Unfriendly

        // Act
        var discount = _service.GetPriceDiscount(saveGame, factionId);

        // Assert
        discount.Should().Be(0.0);
    }

    [Fact]
    public void GetAllReputations_ShouldReturnAllFactions()
    {
        // Arrange
        var saveGame = new SaveGame { PlayerName = "TestHero" };
        _service.GainReputation(saveGame, "merchants-guild", 500);
        _service.GainReputation(saveGame, "thieves-guild", -1000);
        _service.GainReputation(saveGame, "mages-circle", 2000);

        // Act
        var reputations = _service.GetAllReputations(saveGame);

        // Assert
        reputations.Should().HaveCount(3);
        reputations.Should().ContainKey("merchants-guild");
        reputations.Should().ContainKey("thieves-guild");
        reputations.Should().ContainKey("mages-circle");
    }

    [Fact]
    public void GetAllReputations_ShouldReturnEmpty_WhenNoReputations()
    {
        // Arrange
        var saveGame = new SaveGame { PlayerName = "TestHero" };

        // Act
        var reputations = _service.GetAllReputations(saveGame);

        // Assert
        reputations.Should().BeEmpty();
    }
}
