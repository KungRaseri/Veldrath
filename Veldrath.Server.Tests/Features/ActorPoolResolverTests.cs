using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Features.Characters.Combat;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Tests for <see cref="ActorPoolResolver"/>'s failsafe behaviour: missing content is reported and never substituted.
/// </summary>
public class ActorPoolResolverTests
{
    private static ActorPoolResolver CreateResolver(Mock<IEnemyRepository> repo) =>
        new(
            new EnemyGenerator(repo.Object, NullLogger<EnemyGenerator>.Instance),
            NullLogger<ActorPoolResolver>.Instance);

    [Fact]
    public async Task SpawnRosterAsync_Returns_An_Empty_Roster_When_A_Location_Has_No_Pool()
    {
        var repo = new Mock<IEnemyRepository>(MockBehavior.Strict);
        var resolver = CreateResolver(repo);

        var roster = await resolver.SpawnRosterAsync(null);

        roster.Should().BeEmpty("no pool means nothing spawns here — a safe town, not an error");
        repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SpawnRosterAsync_Spawns_The_Configured_Archetype()
    {
        var repo = new Mock<IEnemyRepository>();
        repo.Setup(r => r.GetBySlugAsync("wolf"))
            .ReturnsAsync(new Enemy { Slug = "wolf", Name = "Wolf", Level = 2, MaxHealth = 20 });

        var roster = await CreateResolver(repo).SpawnRosterAsync([new ActorPoolEntry("wolf", 1)]);

        roster.Should().NotBeEmpty();
        roster.Should().OnlyContain(enemy => enemy.ArchetypeSlug == "wolf" && enemy.MaxHealth == 20);
    }

    [Fact]
    public async Task SpawnRosterAsync_Does_Not_Substitute_Anything_For_An_Unknown_Archetype()
    {
        var repo = new Mock<IEnemyRepository>();
        repo.Setup(r => r.GetBySlugAsync(It.IsAny<string>())).ReturnsAsync((Enemy?)null);

        var roster = await CreateResolver(repo).SpawnRosterAsync([new ActorPoolEntry("spectre", 1)]);

        roster.Should().BeEmpty("missing content must fail loudly, not be replaced by a default archetype");
    }

    [Fact]
    public async Task ResolveArchetypeAsync_Returns_Null_For_Missing_Content()
    {
        var repo = new Mock<IEnemyRepository>();
        repo.Setup(r => r.GetBySlugAsync("spectre")).ReturnsAsync((Enemy?)null);

        var template = await CreateResolver(repo).ResolveArchetypeAsync("spectre");

        template.Should().BeNull();
    }

    // ── Placed instances ─────────────────────────────────────────────────────

    [Fact]
    public async Task SpawnRosterAsync_Spawns_Every_Placement_Exactly_Once_With_An_Empty_Pool()
    {
        var repo = new Mock<IEnemyRepository>(MockBehavior.Strict);
        var resolver = CreateResolver(repo);

        var roster = await resolver.SpawnRosterAsync(null, [Chief()]);

        roster.Should().ContainSingle("the placement is content, not a probability");
        var chief = roster[0];
        chief.InstanceSlug.Should().Be("elder-goblin-chief");
        chief.ArchetypeSlug.Should().Be("goblin-scout");
        chief.Name.Should().Be("Elder Goblin Chief");
        chief.Level.Should().Be(5);
        chief.MaxHealth.Should().Be(120);
        chief.BaseXp.Should().Be(80);
        chief.GoldReward.Should().Be(20);
        chief.Template.BasePhysicalDamage.Should().Be(12);
        repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SpawnRosterAsync_Keeps_The_Roster_Between_Two_And_Four_With_Placements_And_A_Pool()
    {
        var repo = new Mock<IEnemyRepository>();
        repo.Setup(r => r.GetBySlugAsync("goblin-scout"))
            .ReturnsAsync(new Enemy { Slug = "goblin-scout", Name = "Goblin Scout", Level = 1, MaxHealth = 30 });
        var resolver = CreateResolver(repo);

        var roster = await resolver.SpawnRosterAsync([new ActorPoolEntry("goblin-scout", 1)], [Chief()]);

        roster.Count.Should().BeInRange(2, 4, "the roster keeps its authored 2–4 size");
        roster.Count(enemy => enemy.InstanceSlug == "elder-goblin-chief").Should().Be(1);
        roster.Should().OnlyContain(enemy => enemy.InstanceSlug == null || enemy.Name == "Elder Goblin Chief");
    }

    [Fact]
    public async Task SpawnRosterAsync_Leaves_A_Non_Hostile_Placement_Out_Of_The_Roster()
    {
        var repo = new Mock<IEnemyRepository>(MockBehavior.Strict);
        var chief = Chief() with
        {
            Template = new Enemy
            {
                Slug = "goblin-scout",
                Name = "Elder Goblin Chief",
                Traits = { ["hostile"] = new TraitValue(false, TraitType.Boolean) },
            },
        };

        var roster = await CreateResolver(repo).SpawnRosterAsync(null, [chief]);

        roster.Should().BeEmpty("a named actor placed by mistake is content debt, not something to attack");
    }

    [Fact]
    public async Task SpawnRosterAsync_Spawns_Nothing_When_There_Is_Neither_Pool_Nor_Placement()
    {
        var repo = new Mock<IEnemyRepository>(MockBehavior.Strict);

        var roster = await CreateResolver(repo).SpawnRosterAsync(null, []);

        roster.Should().BeEmpty("a safe location is authored content, not an error");
        repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ResolveInstanceTemplateAsync_Returns_The_Composed_Template_From_The_Repository()
    {
        var instances = new Mock<IActorInstanceRepository>();
        instances.Setup(r => r.GetTemplateBySlugAsync("elder-goblin-chief", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlacedActorInstance(
                "elder-goblin-chief", "goblin-scout", "Elder Goblin Chief", "boss", "old-clearing",
                new Enemy { Slug = "goblin-scout", BasePhysicalDamage = 12 }));
        var resolver = new ActorPoolResolver(
            new EnemyGenerator(Mock.Of<IEnemyRepository>(), NullLogger<EnemyGenerator>.Instance),
            NullLogger<ActorPoolResolver>.Instance,
            randomSource: null,
            instances.Object);

        var template = await resolver.ResolveInstanceTemplateAsync("elder-goblin-chief");

        template.Should().NotBeNull();
        template!.BasePhysicalDamage.Should().Be(12);
    }

    /// <summary>The authored boss, as the instance repository projects it.</summary>
    private static PlacedActorInstance Chief() => new(
        "elder-goblin-chief",
        "goblin-scout",
        "Elder Goblin Chief",
        "boss",
        "old-clearing",
        new Enemy
        {
            Slug = "goblin-scout",
            Name = "Elder Goblin Chief",
            Level = 5,
            Health = 120,
            MaxHealth = 120,
            Strength = 14,
            BasePhysicalDamage = 12,
            XP = 80,
            GoldReward = 20,
            Traits = { ["hostile"] = new TraitValue(true, TraitType.Boolean) },
        });
}
