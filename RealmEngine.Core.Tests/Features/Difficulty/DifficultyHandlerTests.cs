using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Features.Difficulty.Commands;
using RealmEngine.Core.Features.Difficulty.Queries;
using RealmEngine.Core.Features.Difficulty.Services;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Difficulty;

public class SetDifficultyCommandHandlerTests
{
    private static SetDifficultyCommandHandler CreateHandler(
        ISaveGameService? saveGameService = null,
        IApocalypseTimer? apocalypseTimer = null)
    {
        saveGameService ??= new Mock<ISaveGameService>().Object;
        return new SetDifficultyCommandHandler(
            saveGameService,
            NullLogger<SetDifficultyCommandHandler>.Instance,
            apocalypseTimer);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoActiveSave()
    {
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new SetDifficultyCommand { DifficultyName = "Normal" }, default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("No active game session");
    }

    [Theory]
    [InlineData("Easy")]
    [InlineData("Normal")]
    [InlineData("Hard")]
    [InlineData("Expert")]
    [InlineData("Ironman")]
    [InlineData("Permadeath")]
    public async Task Handle_SetsKnownDifficulty_ReturnsSuccess(string difficultyName)
    {
        var saveGame = new SaveGame { PlayerName = "Hero", Character = new Character() };
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new SetDifficultyCommand { DifficultyName = difficultyName }, default);

        result.Success.Should().BeTrue();
        result.DifficultyName.Should().Be(difficultyName);
        saveGame.DifficultyLevel.Should().Be(difficultyName);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenUnknownDifficulty()
    {
        var saveGame = new SaveGame { PlayerName = "Hero", Character = new Character() };
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new SetDifficultyCommand { DifficultyName = "SuperHard" }, default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("SuperHard");
    }

    [Fact]
    public async Task Handle_ApocalypseMode_StartsTimer_WhenTimerProvided()
    {
        var saveGame = new SaveGame { PlayerName = "Hero", Character = new Character() };
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var mockTimer = new Mock<IApocalypseTimer>();
        var handler = CreateHandler(mockSave.Object, mockTimer.Object);

        var result = await handler.Handle(new SetDifficultyCommand { DifficultyName = "Apocalypse" }, default);

        result.Success.Should().BeTrue();
        result.ApocalypseModeEnabled.Should().BeTrue();
        result.ApocalypseTimeLimitMinutes.Should().Be(240);
        mockTimer.Verify(t => t.Start(), Times.Once);
    }

    [Fact]
    public async Task Handle_ApocalypseMode_ReturnsSuccess_WhenNoTimerProvided()
    {
        var saveGame = new SaveGame { PlayerName = "Hero", Character = new Character() };
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new SetDifficultyCommand { DifficultyName = "Apocalypse" }, default);

        result.Success.Should().BeTrue();
        result.ApocalypseModeEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NonApocalypseMode_DoesNotSetTimeLimitMinutes()
    {
        var saveGame = new SaveGame { PlayerName = "Hero", Character = new Character() };
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new SetDifficultyCommand { DifficultyName = "Normal" }, default);

        result.Success.Should().BeTrue();
        result.ApocalypseModeEnabled.Should().BeFalse();
        result.ApocalypseTimeLimitMinutes.Should().BeNull();
    }
}

public class GetAvailableDifficultiesQueryHandlerTests
{
    private static GetAvailableDifficultiesQueryHandler CreateHandler(ISaveGameService? saveGameService = null)
    {
        saveGameService ??= new Mock<ISaveGameService>().Object;
        return new GetAvailableDifficultiesQueryHandler(saveGameService);
    }

    [Fact]
    public async Task Handle_ReturnsAllSevenDifficulties()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new GetAvailableDifficultiesQuery(), default);

        result.Difficulties.Should().HaveCount(7);
        result.Difficulties.Select(d => d.Name).Should()
            .Contain(["Easy", "Normal", "Hard", "Expert", "Ironman", "Permadeath", "Apocalypse"]);
    }

    [Fact]
    public async Task Handle_ReturnsNormalAsCurrentDifficulty_WhenNoActiveSave()
    {
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new GetAvailableDifficultiesQuery(), default);

        result.CurrentDifficulty.Should().Be("Normal");
    }

    [Fact]
    public async Task Handle_ReturnsCurrentSaveDifficulty_WhenActiveSaveExists()
    {
        var saveGame = new SaveGame { DifficultyLevel = "Hard", Character = new Character() };
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new GetAvailableDifficultiesQuery(), default);

        result.CurrentDifficulty.Should().Be("Hard");
    }
}

public class GetDifficultySettingsQueryHandlerTests
{
    private static GetDifficultySettingsQueryHandler CreateHandler(ISaveGameService? saveGameService = null)
    {
        saveGameService ??= new Mock<ISaveGameService>().Object;
        return new GetDifficultySettingsQueryHandler(saveGameService);
    }

    [Fact]
    public async Task Handle_ReturnsDifficultySettingsFromService()
    {
        var expected = DifficultySettings.Hard;
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetDifficultySettings()).Returns(expected);
        var handler = CreateHandler(mockSave.Object);

        var result = await handler.Handle(new GetDifficultySettingsQuery(), default);

        result.Name.Should().Be("Hard");
        result.EnemyDamageMultiplier.Should().Be(1.25);
    }

    [Fact]
    public async Task Handle_DelegatesDifficultyLookupToSaveGameService()
    {
        var mockSave = new Mock<ISaveGameService>();
        mockSave.Setup(s => s.GetDifficultySettings()).Returns(DifficultySettings.Normal);
        var handler = CreateHandler(mockSave.Object);

        await handler.Handle(new GetDifficultySettingsQuery(), default);

        mockSave.Verify(s => s.GetDifficultySettings(), Times.Once);
    }
}

public class DifficultyServiceTests
{
    private readonly DifficultyService _service = new();

    [Theory]
    [InlineData(100, 1.0, 100)]
    [InlineData(100, 1.5, 150)]
    [InlineData(100, 0.5, 50)]
    [InlineData(33, 1.5, 50)]  // rounds to nearest
    public void CalculatePlayerDamage_AppliesMultiplierAndRounds(int baseDamage, double multiplier, int expected)
    {
        var difficulty = new DifficultySettings { PlayerDamageMultiplier = multiplier };

        var result = _service.CalculatePlayerDamage(baseDamage, difficulty);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(100, 1.0, 100)]
    [InlineData(100, 1.25, 125)]
    [InlineData(100, 0.75, 75)]
    public void CalculateEnemyDamage_AppliesMultiplierAndRounds(int baseDamage, double multiplier, int expected)
    {
        var difficulty = new DifficultySettings { EnemyDamageMultiplier = multiplier };

        var result = _service.CalculateEnemyDamage(baseDamage, difficulty);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(200, 1.0, 200)]
    [InlineData(200, 1.5, 300)]
    [InlineData(200, 0.75, 150)]
    public void CalculateEnemyHealth_AppliesMultiplierAndRounds(int baseHealth, double multiplier, int expected)
    {
        var difficulty = new DifficultySettings { EnemyHealthMultiplier = multiplier };

        var result = _service.CalculateEnemyHealth(baseHealth, difficulty);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(100, 1.0, 100)]
    [InlineData(100, 1.5, 150)]
    [InlineData(100, 0.5, 50)]
    public void CalculateGoldReward_AppliesGoldXPMultiplier(int baseGold, double multiplier, int expected)
    {
        var difficulty = new DifficultySettings { GoldXPMultiplier = multiplier };

        var result = _service.CalculateGoldReward(baseGold, difficulty);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(100, 1.0, 100)]
    [InlineData(100, 1.5, 150)]
    [InlineData(100, 0.5, 50)]
    public void CalculateXPReward_AppliesGoldXPMultiplier(int baseXP, double multiplier, int expected)
    {
        var difficulty = new DifficultySettings { GoldXPMultiplier = multiplier };

        var result = _service.CalculateXPReward(baseXP, difficulty);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(1000, 0.10, 100)]
    [InlineData(1000, 0.25, 250)]
    [InlineData(1000, 0.0, 0)]
    [InlineData(100, 0.30, 30)]
    public void CalculateGoldLoss_AppliesLossPercentage(int currentGold, double lossPercent, int expected)
    {
        var difficulty = new DifficultySettings { GoldLossPercentage = lossPercent };

        var result = _service.CalculateGoldLoss(currentGold, difficulty);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(1000, 0.25, 250)]
    [InlineData(1000, 0.50, 500)]
    [InlineData(1000, 0.0, 0)]
    public void CalculateXPLoss_AppliesLossPercentage(int currentXP, double lossPercent, int expected)
    {
        var difficulty = new DifficultySettings { XPLossPercentage = lossPercent };

        var result = _service.CalculateXPLoss(currentXP, difficulty);

        result.Should().Be(expected);
    }

    [Fact]
    public void CanManualSave_ReturnsTrue_WhenAutoSaveOnlyIsFalse()
    {
        var difficulty = new DifficultySettings { AutoSaveOnly = false };

        _service.CanManualSave(difficulty).Should().BeTrue();
    }

    [Fact]
    public void CanManualSave_ReturnsFalse_WhenAutoSaveOnlyIsTrue()
    {
        var difficulty = new DifficultySettings { AutoSaveOnly = true };

        _service.CanManualSave(difficulty).Should().BeFalse();
    }

    [Fact]
    public void IsPermadeath_ReflectsDifficultyFlag()
    {
        _service.IsPermadeath(DifficultySettings.Permadeath).Should().BeTrue();
        _service.IsPermadeath(DifficultySettings.Normal).Should().BeFalse();
    }

    [Fact]
    public void IsApocalypseMode_ReflectsDifficultyFlag()
    {
        _service.IsApocalypseMode(DifficultySettings.Apocalypse).Should().BeTrue();
        _service.IsApocalypseMode(DifficultySettings.Hard).Should().BeFalse();
    }

    [Fact]
    public void GetDifficultySummary_ContainsRequiredKeys()
    {
        var summary = _service.GetDifficultySummary(DifficultySettings.Normal);

        summary.Should().ContainKey("Name");
        summary.Should().ContainKey("Description");
        summary.Should().ContainKey("Player Damage");
        summary.Should().ContainKey("Enemy Damage");
        summary.Should().ContainKey("Enemy Health");
        summary.Should().ContainKey("Gold/XP Gain");
        summary.Should().ContainKey("Gold Loss on Death");
        summary.Should().ContainKey("XP Loss on Death");
    }

    [Fact]
    public void GetDifficultySummary_IncludesSaveMode_ForIronman()
    {
        var summary = _service.GetDifficultySummary(DifficultySettings.Ironman);

        summary.Should().ContainKey("Save Mode");
        summary["Save Mode"].Should().Contain("Ironman");
    }

    [Fact]
    public void GetDifficultySummary_IncludesPermadeath_ForPermdeath()
    {
        var summary = _service.GetDifficultySummary(DifficultySettings.Permadeath);

        summary.Should().ContainKey("Permadeath");
    }

    [Fact]
    public void GetDifficultySummary_IncludesTimeLimit_ForApocalypse()
    {
        var summary = _service.GetDifficultySummary(DifficultySettings.Apocalypse);

        summary.Should().ContainKey("Time Limit");
        summary["Time Limit"].Should().Contain("240");
    }

    [Fact]
    public void GetDifficultySummary_IncludesItemDropInfo_WhenItemsDropped()
    {
        var summary = _service.GetDifficultySummary(DifficultySettings.Normal);

        summary.Should().ContainKey("Item Loss");
        summary["Item Loss"].Should().Contain("1");
    }

    [Fact]
    public void GetDifficultySummary_IndicatesNoItems_WhenEasyMode()
    {
        var summary = _service.GetDifficultySummary(DifficultySettings.Easy);

        summary.Should().ContainKey("Item Loss");
        summary["Item Loss"].Should().Contain("No items");
    }

    [Fact]
    public void GetDifficultySummary_IndicatesDropAll_WhenHardMode()
    {
        var summary = _service.GetDifficultySummary(DifficultySettings.Hard);

        summary.Should().ContainKey("Item Loss");
        summary["Item Loss"].Should().Contain("ALL");
    }
}
