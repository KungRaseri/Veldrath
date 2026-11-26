using System.Reflection;
using RealmEngine.Data.Entities;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Tests.Repositories;

/// <summary>
/// Makes the archetype projection contract mechanical: every <see cref="ArchetypeStats"/> property is either projected
/// onto the spawned template or reserved with a stated reason, and a stat that is neither fails here.
/// </summary>
/// <remarks>
/// Without this guard, adding a stat to <see cref="ArchetypeStats"/> silently does nothing — content authors it, combat
/// never sees it — which is exactly the defect E1 fixed for the stats that already existed. The second assertion is the
/// other direction of the same decision: a reserved stat must stay off <see cref="Enemy"/>, so if a later card adds
/// <c>Enemy.ArmorClass</c> this test forces the reservation to be revisited on purpose instead of by drift.
/// </remarks>
[Trait("Category", "Repository")]
public class ArchetypeStatsProjectionClassificationTests
{
    /// <summary>Stats <c>EfCoreEnemyRepository.MapToModel</c> assigns onto the spawned template.</summary>
    private static readonly string[] Projected =
    [
        nameof(ArchetypeStats.Health),
        nameof(ArchetypeStats.Mana),
        nameof(ArchetypeStats.Strength),
        nameof(ArchetypeStats.Agility),
        nameof(ArchetypeStats.Intelligence),
        nameof(ArchetypeStats.Constitution),
        nameof(ArchetypeStats.Damage),
        nameof(ArchetypeStats.ExperienceReward),
        nameof(ArchetypeStats.GoldRewardMin),
        nameof(ArchetypeStats.GoldRewardMax),
    ];

    /// <summary>Stats authored on the archetype and deliberately never projected, each with the reason it is not.</summary>
    private static readonly Dictionary<string, string> Reserved = new()
    {
        [nameof(ArchetypeStats.ArmorClass)] =
            "Reserved: the template mitigates physical damage through Constitution, so a projected armor class would " +
            "subtract twice for one defence, and an authored 1–16 would floor the live damage formula at 1.",
        [nameof(ArchetypeStats.AttackBonus)] =
            "Reserved: no attack roll exists on either combat path — the engine roll is the player's dodge and the " +
            "server path lands damage unconditionally — so a hit bonus has nothing to modify.",
        [nameof(ArchetypeStats.TradeSkill)] =
            "Reserved: a shopkeeper field with no consumer on the spawn path; shop behaviour reads the archetype row, " +
            "not the combat template.",
        [nameof(ArchetypeStats.TradeGold)] =
            "Reserved: a shopkeeper field with no consumer on the spawn path; the template's reward is GoldReward.",
    };

    /// <summary>The reserved stats, as data for the reflection theory.</summary>
    public static IEnumerable<object[]> ReservedStats =>
        Reserved.Select(entry => new object[] { entry.Key, entry.Value });

    /// <summary>The reserved stat names, as data for the no-member theory.</summary>
    public static IEnumerable<object[]> ReservedStatNames =>
        Reserved.Keys.Select(name => new object[] { name });

    [Fact]
    public void EveryArchetypeStatIsClassifiedAsProjectedOrReserved()
    {
        var declared = typeof(ArchetypeStats)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var classified = Projected
            .Concat(Reserved.Keys)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        // Equal on the ordered arrays, so a stat with no classification fails as does a classification naming a stat
        // that no longer exists, and a duplicate cannot hide behind set semantics.
        declared.Should().Equal(
            classified,
            "a stat that is neither projected nor reserved reaches no consumer at all — classify it, with a reason, " +
            "when you add it");
    }

    [Fact]
    public void TheReservedSetIsTheFourAuthoredButUnprojectedStats()
    {
        Reserved.Keys.Should().BeEquivalentTo(
            [nameof(ArchetypeStats.ArmorClass), nameof(ArchetypeStats.AttackBonus), nameof(ArchetypeStats.TradeSkill), nameof(ArchetypeStats.TradeGold)],
            "this is the decision D1 recorded: four authored stats are reserved on purpose");
        Reserved.Values.Should().OnlyContain(
            reason => !string.IsNullOrWhiteSpace(reason),
            "a reserved stat without a written reason is an oversight wearing the word 'reserved'");
    }

    [Theory]
    [MemberData(nameof(ReservedStats))]
    public void AReservedStatCarriesAReason(string name, string reason)
    {
        name.Should().NotBeNullOrWhiteSpace();
        reason.Should().NotBeNullOrWhiteSpace($"{name} is reserved, so the reason has to be written down");
    }

    [Theory]
    [MemberData(nameof(ReservedStatNames))]
    public void AReservedStatIsNotAMemberOfTheEnemyTemplate(string name)
    {
        // If this fails, someone put the stat on the template: either the reservation is wrong and the decision has to
        // be revisited, or the member has to go. It must never become true by drift.
        typeof(Enemy).GetProperty(name).Should().BeNull(
            $"{name} is reserved and unprojected; adding it to Enemy would wire it into combat without a decision");
    }
}
