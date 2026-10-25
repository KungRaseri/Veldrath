using Veldrath.Server.Features.Characters.Combat;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Tests for the reward split a party kill depends on. The invariant that matters is that the parts always add
/// up to the whole: rounding must never mint or lose experience and gold.
/// </summary>
public class CombatRewardsTests
{
    [Fact]
    public void SplitByWeight_Gives_A_Solo_Contributor_The_Whole_Reward()
    {
        CombatRewards.SplitByWeight(120, [37]).Should().Equal(120);
    }

    [Fact]
    public void SplitByWeight_Hands_The_Leftover_Unit_To_The_Largest_Remainder()
    {
        // 100 across three equal weights is 33 each plus one unit spare; the spare goes to the first contributor.
        var shares = CombatRewards.SplitByWeight(100, [1, 1, 1]);

        shares.Should().Equal(34, 33, 33);
        shares.Sum().Should().Be(100);
    }

    [Fact]
    public void SplitByWeight_Pays_In_Proportion_To_Damage()
    {
        CombatRewards.SplitByWeight(100, [30, 10]).Should().Equal(75, 25);
    }

    [Fact]
    public void SplitByWeight_Never_Pays_A_Zero_Contributor()
    {
        var shares = CombatRewards.SplitByWeight(10, [0, 5]);

        shares.Should().Equal(0, 10);
    }

    [Fact]
    public void SplitByWeight_Returns_Nothing_When_The_Reward_Or_Every_Weight_Is_Zero()
    {
        CombatRewards.SplitByWeight(0, [3, 7]).Should().Equal(0, 0);
        CombatRewards.SplitByWeight(50, [0, 0]).Should().Equal(0, 0);
        CombatRewards.SplitByWeight(50, []).Should().BeEmpty();
    }

    [Fact]
    public void Split_Pays_Both_Pools_Over_The_Same_Contributions()
    {
        var killer = Guid.NewGuid();
        var ally = Guid.NewGuid();

        var shares = CombatRewards.Split(10, 5, [(killer, 15), (ally, 30)]);

        shares.Should().HaveCount(2);
        shares.Sum(share => share.Xp).Should().Be(10);
        shares.Sum(share => share.Gold).Should().Be(5);
        shares.Single(share => share.CharacterId == ally).Xp.Should().BeGreaterThan(
            shares.Single(share => share.CharacterId == killer).Xp,
            "the ally dealt twice the damage");
    }
}
