using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Settings;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Tests for the gold-first death penalty: exactly one penalty per death, capped, and never applied to a character
/// holding nothing.
/// </summary>
public class DeathPenaltiesTests
{
    private static readonly DeathPenaltyOptions Defaults = new();

    [Fact]
    public void Takes_Gold_Only_When_The_Character_Carries_Enough()
    {
        var penalty = DeathPenalties.Calculate(100, 1_000, Defaults);

        penalty.GoldLost.Should().Be(10, "10% of 100 gold");
        penalty.XpLost.Should().Be(0, "gold is taken first");
    }

    [Fact]
    public void Takes_Xp_Only_When_The_Character_Is_Below_The_Gold_Floor()
    {
        var penalty = DeathPenalties.Calculate(9, 1_000, Defaults);

        penalty.GoldLost.Should().Be(0, "9 gold is below the floor of 10");
        penalty.XpLost.Should().Be(50, "5% of 1000 experience");
    }

    [Fact]
    public void Takes_Nothing_From_A_Character_Holding_Neither()
    {
        DeathPenalties.Calculate(0, 0, Defaults).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Respects_The_Absolute_Gold_Cap()
    {
        var penalty = DeathPenalties.Calculate(1_000_000, 0, new DeathPenaltyOptions { GoldLossCap = 100 });

        penalty.GoldLost.Should().Be(100, "one death must never take a fortune");
    }

    [Fact]
    public void Always_Takes_At_Least_One_Unit_So_A_Small_Balance_Still_Notices()
    {
        DeathPenalties.Calculate(10, 0, Defaults).GoldLost.Should().Be(1, "10% of 10 rounds up to 1");
    }

    [Fact]
    public void Sickness_Reduces_Damage_Only_While_Its_Window_Is_Open()
    {
        var now = DateTimeOffset.UtcNow;
        var attrs = new Dictionary<string, int>
        {
            [DeathPenalties.SicknessUntilKey] = DeathPenalties.ToSicknessStamp(now.AddMinutes(5)),
            [DeathPenalties.SicknessPenaltyKey] = 25,
        };

        DeathPenalties.IsSicknessActive(attrs, now).Should().BeTrue();
        DeathPenalties.ApplySicknessToDamage(100, attrs, now).Should().Be(75);
        DeathPenalties.IsSicknessActive(attrs, now.AddMinutes(6)).Should().BeFalse("the window has closed");
        DeathPenalties.ApplySicknessToDamage(100, attrs, now.AddMinutes(6)).Should().Be(100);
    }

    [Fact]
    public void Sickness_Never_Removes_The_Last_Point_Of_Damage()
    {
        var now = DateTimeOffset.UtcNow;
        var attrs = new Dictionary<string, int>
        {
            [DeathPenalties.SicknessUntilKey] = DeathPenalties.ToSicknessStamp(now.AddMinutes(5)),
            [DeathPenalties.SicknessPenaltyKey] = 90,
        };

        DeathPenalties.ApplySicknessToDamage(4, attrs, now).Should().Be(1, "a weakened character must still be able to fight");
    }
}
