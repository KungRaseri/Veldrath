using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Services;
using Xunit;

namespace RealmEngine.Core.Tests.Services;

[Trait("Category", "Services")]
public class ApocalypseTimerTests
{
    private static ApocalypseTimer CreateTimer() =>
        new(NullLogger<ApocalypseTimer>.Instance);

    // Start / initial state
    [Fact]
    public void Start_JustStarted_RemainingMinutesNearFourHours()
    {
        var timer = CreateTimer();
        timer.Start();
        timer.GetRemainingMinutes().Should().BeInRange(239, 240);
    }

    [Fact]
    public void Start_JustStarted_IsNotExpired()
    {
        var timer = CreateTimer();
        timer.Start();
        timer.IsExpired().Should().BeFalse();
    }

    // StartFromSave
    [Fact]
    public void StartFromSave_TimerElapsedBeyondLimit_IsExpired()
    {
        var timer = CreateTimer();
        timer.StartFromSave(DateTime.Now.AddMinutes(-500), bonusMinutes: 0);
        timer.IsExpired().Should().BeTrue();
        timer.GetRemainingMinutes().Should().Be(0);
    }

    [Fact]
    public void StartFromSave_WithBonusMinutes_ExtendsEffectiveDuration()
    {
        var timer = CreateTimer();
        // Started 230 minutes ago: without bonus → 10 min left; with +30 → ~40 min left
        timer.StartFromSave(DateTime.Now.AddMinutes(-230), bonusMinutes: 30);
        timer.GetRemainingMinutes().Should().BeInRange(38, 42);
    }

    // Pause / Resume
    [Fact]
    public void GetRemainingMinutes_WhilePaused_DoesNotAdvance()
    {
        var timer = CreateTimer();
        timer.Start();
        timer.Pause();

        var first = timer.GetRemainingMinutes();
        var second = timer.GetRemainingMinutes();

        second.Should().Be(first);
    }

    [Fact]
    public void Pause_WhenAlreadyPaused_DoesNotDoublePause()
    {
        var timer = CreateTimer();
        timer.Start();
        timer.Pause();
        timer.Pause(); // second call should be a no-op
        timer.Resume();
        timer.GetRemainingMinutes().Should().BeInRange(239, 240);
    }

    [Fact]
    public void Resume_WithoutPause_DoesNotThrow()
    {
        var timer = CreateTimer();
        timer.Start();
        var act = timer.Resume;
        act.Should().NotThrow();
    }

    // AddBonusTime
    [Fact]
    public void AddBonusTime_IncreasesRemainingMinutes()
    {
        var timer = CreateTimer();
        timer.Start();
        var before = timer.GetRemainingMinutes();
        timer.AddBonusTime(30, "Quest complete");
        timer.GetRemainingMinutes().Should().BeGreaterThan(before);
    }

    [Fact]
    public void AddBonusTime_ReturnsCorrectResult()
    {
        var timer = CreateTimer();
        timer.Start();
        var result = timer.AddBonusTime(30, "Quest complete");
        result.MinutesAdded.Should().Be(30);
        result.Reason.Should().Be("Quest complete");
        result.TotalRemainingMinutes.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AddBonusTime_DefaultReason_IsQuestCompleted()
    {
        var timer = CreateTimer();
        timer.Start();
        var result = timer.AddBonusTime(10);
        result.Reason.Should().Be("Quest completed");
    }

    [Fact]
    public void AddBonusTime_ToExpiredTimer_TimerBecomesActive()
    {
        var timer = CreateTimer();
        // Started 280 min ago: elapsed(280) > total(240) → expired by 40 min
        timer.StartFromSave(DateTime.Now.AddMinutes(-280), bonusMinutes: 0);
        timer.IsExpired().Should().BeTrue();

        // Adding 60 bonus: remaining = 240 + 60 - ~280 = ~20 min left
        timer.AddBonusTime(60, "Revival");

        timer.IsExpired().Should().BeFalse();
    }

    // GetFormattedTimeRemaining
    [Theory]
    [InlineData(-120, "2h 0m")]   // 240 - 120 = 120 min remaining
    [InlineData(-150, "1h 30m")]  // 240 - 150 = 90 min remaining
    [InlineData(-185, "0h 55m")]  // 240 - 185 = 55 min remaining
    [InlineData(-239, "0h 1m")]   // 240 - 239 = 1 min remaining
    public void GetFormattedTimeRemaining_VariousElapsed_FormatsCorrectly(int offsetMinutes, string expected)
    {
        var timer = CreateTimer();
        // AddSeconds(30) provides headroom so sub-minute elapsed drift doesn't push
        // the integer truncation across the minute boundary.
        timer.StartFromSave(DateTime.Now.AddMinutes(offsetMinutes).AddSeconds(30), bonusMinutes: 0);
        timer.GetFormattedTimeRemaining().Should().Be(expected);
    }

    // GetColoredTimeDisplay
    [Fact]
    public void GetColoredTimeDisplay_OverSixtyMinutes_IsGreen()
    {
        var timer = CreateTimer();
        timer.StartFromSave(DateTime.Now.AddMinutes(-60), bonusMinutes: 0); // ~180 remaining
        timer.GetColoredTimeDisplay().Should().Contain("[green]");
    }

    [Fact]
    public void GetColoredTimeDisplay_BetweenThirtyAndSixtyMinutes_IsOrange()
    {
        var timer = CreateTimer();
        timer.StartFromSave(DateTime.Now.AddMinutes(-205), bonusMinutes: 0); // ~35 remaining
        timer.GetColoredTimeDisplay().Should().Contain("[orange]");
    }

    [Fact]
    public void GetColoredTimeDisplay_BetweenTenAndThirtyMinutes_IsYellow()
    {
        var timer = CreateTimer();
        timer.StartFromSave(DateTime.Now.AddMinutes(-215), bonusMinutes: 0); // ~25 remaining
        timer.GetColoredTimeDisplay().Should().Contain("[yellow]");
    }

    [Fact]
    public void GetColoredTimeDisplay_UnderTenMinutes_IsRed()
    {
        var timer = CreateTimer();
        timer.StartFromSave(DateTime.Now.AddMinutes(-235), bonusMinutes: 0); // ~5 remaining
        timer.GetColoredTimeDisplay().Should().Contain("[red]");
    }

    [Fact]
    public void GetColoredTimeDisplay_ContainsFormattedTime()
    {
        var timer = CreateTimer();
        // AddSeconds(30) keeps remaining within the "120 minutes" integer window
        timer.StartFromSave(DateTime.Now.AddMinutes(-120).AddSeconds(30), bonusMinutes: 0);
        timer.GetColoredTimeDisplay().Should().Contain("2h 0m");
    }

    // CheckTimeWarnings
    [Fact]
    public void CheckTimeWarnings_FreshTimer_ReturnsNull()
    {
        var timer = CreateTimer();
        timer.Start();
        timer.CheckTimeWarnings().Should().BeNull();
    }

    [Fact]
    public void CheckTimeWarnings_FiftyFiveMinutesRemaining_ReturnsOneHourWarning()
    {
        var timer = CreateTimer();
        timer.StartFromSave(DateTime.Now.AddMinutes(-185), bonusMinutes: 0); // ~55 min remaining
        var result = timer.CheckTimeWarnings();
        result.Should().NotBeNull();
        result!.Title.Should().Contain("HOUR");
    }

    [Fact]
    public void CheckTimeWarnings_OneHourWarningAlreadyShown_ReturnsNull()
    {
        var timer = CreateTimer();
        timer.StartFromSave(DateTime.Now.AddMinutes(-185), bonusMinutes: 0);
        timer.CheckTimeWarnings(); // fires one-hour warning, sets flag
        timer.CheckTimeWarnings().Should().BeNull();
    }

    [Fact]
    public void CheckTimeWarnings_TwentyFiveMinutesRemaining_ReturnsThirtyMinWarning()
    {
        var timer = CreateTimer();
        timer.StartFromSave(DateTime.Now.AddMinutes(-215), bonusMinutes: 0); // ~25 min remaining
        timer.CheckTimeWarnings(); // consume the 60-min warning (≤60 triggers first)
        var result = timer.CheckTimeWarnings();
        result.Should().NotBeNull();
        result!.Title.Should().Contain("30");
    }

    [Fact]
    public void CheckTimeWarnings_FiveMinutesRemaining_ReturnsTenMinWarning()
    {
        var timer = CreateTimer();
        timer.StartFromSave(DateTime.Now.AddMinutes(-235), bonusMinutes: 0); // ~5 min remaining
        timer.CheckTimeWarnings(); // consume 60-min warning
        timer.CheckTimeWarnings(); // consume 30-min warning
        var result = timer.CheckTimeWarnings();
        result.Should().NotBeNull();
        result!.Title.Should().Contain("10");
    }

    [Fact]
    public void CheckTimeWarnings_AllWarningsAlreadyShown_ReturnsNull()
    {
        var timer = CreateTimer();
        timer.StartFromSave(DateTime.Now.AddMinutes(-235), bonusMinutes: 0);
        timer.CheckTimeWarnings(); // 60-min
        timer.CheckTimeWarnings(); // 30-min
        timer.CheckTimeWarnings(); // 10-min
        timer.CheckTimeWarnings().Should().BeNull(); // all flags set
    }
}
