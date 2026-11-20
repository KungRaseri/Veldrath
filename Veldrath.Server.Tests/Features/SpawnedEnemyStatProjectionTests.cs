using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Repositories;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Features.Characters.Combat;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Spawn-level acceptance test for E1: the stats the band table authors have to reach the enemy a player fights,
/// not just the content row. These run the real seeded catalog through the real projection and the real spawn path.
/// </summary>
/// <remarks>
/// Every assertion here read a template default before the projection carried stats — 50 max health,
/// 5 base damage, 25 XP, 10 gold — for all three bands, which made the published band table decorative.
/// </remarks>
[Trait("Category", "Combat")]
public class SpawnedEnemyStatProjectionTests
{
    private const int Seed = 20260101;

    [Fact]
    public async Task The_high_band_fire_ancient_spawns_with_more_than_the_template_defaults()
    {
        await using var probe = new SeededContentProbe();

        var roster = await SpawnAsync(probe, "fire-ancient");

        roster.Should().NotBeEmpty("fire-ancient is a hostile archetype, so a pool naming it has to spawn it");
        roster.Should().OnlyContain(enemy => enemy.MaxHealth > 50, "50 is the template default, not 900 authored health");
        roster.Should().OnlyContain(enemy => enemy.CurrentHealth == enemy.MaxHealth, "a spawn arrives at full health");
        roster.Should().OnlyContain(enemy => enemy.Template.BasePhysicalDamage > 5, "5 is the template default, not 42 authored damage");
        roster.Should().OnlyContain(enemy => enemy.BaseXp > 25, "25 is the template default, not 1200 authored XP");
        roster.Should().OnlyContain(enemy => enemy.GoldReward > 10, "10 is the template default; the band authors 150–300");

        // The authored numbers themselves, not merely "more than the default".
        roster.Should().OnlyContain(enemy => enemy.MaxHealth == 900);
        roster.Should().OnlyContain(enemy => enemy.Template.BasePhysicalDamage == 42);
        roster.Should().OnlyContain(enemy => enemy.BaseXp == 1200);
        roster.Should().OnlyContain(enemy => enemy.Template.MaxMana == 200);
        roster.Should().OnlyContain(enemy => enemy.GoldReward >= 150 && enemy.GoldReward <= 300);
    }

    [Fact]
    public async Task The_level_one_giant_rat_spawns_with_its_authored_twenty_health()
    {
        await using var probe = new SeededContentProbe();

        var roster = await SpawnAsync(probe, "giant-rat");

        roster.Should().NotBeEmpty();
        roster.Should().OnlyContain(enemy => enemy.MaxHealth == 20, "the archetype authors 20 health, not the template's 50");
        roster.Should().OnlyContain(enemy => enemy.CurrentHealth == 20);
        roster.Should().OnlyContain(enemy => enemy.Template.BasePhysicalDamage == 3);
        roster.Should().OnlyContain(enemy => enemy.BaseXp == 8);
        roster.Should().OnlyContain(enemy => enemy.GoldReward >= 1 && enemy.GoldReward <= 3);
    }

    [Fact]
    public async Task Rewards_and_health_differ_between_two_archetypes_in_different_bands()
    {
        await using var lowProbe = new SeededContentProbe();
        await using var midProbe = new SeededContentProbe();
        await using var highProbe = new SeededContentProbe();

        var rat = await SpawnAsync(lowProbe, "giant-rat");
        var ruffian = await SpawnAsync(midProbe, "bandit-ruffian");
        var ancient = await SpawnAsync(highProbe, "fire-ancient");

        var ratMaxXp = rat.Max(enemy => enemy.BaseXp);
        var ruffianMinXp = ruffian.Min(enemy => enemy.BaseXp);
        var ancientMinXp = ancient.Min(enemy => enemy.BaseXp);

        ratMaxXp.Should().BeLessThan(ruffianMinXp, "band 1–3 must not pay what band 3–6 pays");
        ruffianMinXp.Should().BeLessThan(ancientMinXp, "band 3–6 must not pay what band 26–30 pays");

        // The authored gold ranges do not overlap across these bands, so the roll cannot blur them together.
        rat.Max(enemy => enemy.GoldReward).Should().BeLessThan(ruffian.Min(enemy => enemy.GoldReward));
        ruffian.Max(enemy => enemy.GoldReward).Should().BeLessThan(ancient.Min(enemy => enemy.GoldReward));

        rat.Max(enemy => enemy.MaxHealth).Should().BeLessThan(ruffian.Min(enemy => enemy.MaxHealth));
        ruffian.Max(enemy => enemy.MaxHealth).Should().BeLessThan(ancient.Min(enemy => enemy.MaxHealth));

        // The middle band, spelled out, so the before/after table in the change is pinned by a test and not by prose.
        ruffian.Should().OnlyContain(enemy => enemy.MaxHealth == 55);
        ruffian.Should().OnlyContain(enemy => enemy.Template.BasePhysicalDamage == 7);
        ruffian.Should().OnlyContain(enemy => enemy.BaseXp == 40);
        ruffian.Should().OnlyContain(enemy => enemy.GoldReward >= 5 && enemy.GoldReward <= 15);
    }

    [Fact]
    public async Task The_same_seed_spawns_the_same_rewards()
    {
        await using var first = new SeededContentProbe();
        await using var second = new SeededContentProbe();

        var firstRoster = await SpawnAsync(first, "bandit-ruffian");
        var secondRoster = await SpawnAsync(second, "bandit-ruffian");

        firstRoster.Select(enemy => (enemy.Name, enemy.MaxHealth, enemy.BaseXp, enemy.GoldReward))
            .Should().Equal(secondRoster.Select(enemy => (enemy.Name, enemy.MaxHealth, enemy.BaseXp, enemy.GoldReward)),
                "a fixed seed has to reproduce the spawn, gold roll included");
    }

    /// <summary>
    /// Seeds a private catalog with the shipping content and spawns one archetype through the real resolver,
    /// giving the repository and the resolver the same seeded random source the composition root gives them.
    /// </summary>
    /// <param name="probe">Seeded content fixture.</param>
    /// <param name="archetypeSlug">Archetype to spawn.</param>
    /// <returns>The spawned roster.</returns>
    private static async Task<List<SpawnedEnemy>> SpawnAsync(SeededContentProbe probe, string archetypeSlug)
    {
        ContentDbContext content = await probe.CreateContextAsync();
        IRandomSource random = FakeRandomSource.Seeded(Seed);

        var repository = new EfCoreEnemyRepository(content, NullLogger<EfCoreEnemyRepository>.Instance, random);
        var resolver = new ActorPoolResolver(
            new EnemyGenerator(repository, NullLogger<EnemyGenerator>.Instance, random),
            NullLogger<ActorPoolResolver>.Instance,
            random);

        return await resolver.SpawnRosterAsync([new ActorPoolEntry(archetypeSlug, 100)]);
    }
}
