using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Repositories;
using RealmEngine.Shared.Abstractions;

namespace RealmEngine.Data.Tests.Repositories;

/// <summary>
/// Guards the archetype → <see cref="RealmEngine.Shared.Models.Enemy"/> projection: every stat the band table authors
/// has to reach the spawned template, because combat reads the template and never the content row.
/// </summary>
/// <remarks>
/// These assertions are the acceptance test for E1. Before the projection carried stats, each one of them read the
/// <see cref="RealmEngine.Shared.Models.Enemy"/> defaults — 50 max health, 5 base damage, 25 XP, 10 gold — no matter
/// what the archetype authored.
/// </remarks>
[Trait("Category", "Repository")]
public class EfCoreEnemyRepositoryStatProjectionTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ActorArchetype MakeArchetype(string slug, ArchetypeStats stats, int minLevel = 3) =>
        new()
        {
            Slug = slug,
            TypeKey = "beasts",
            DisplayName = slug,
            IsActive = true,
            MinLevel = minLevel,
            MaxLevel = minLevel + 3,
            Stats = stats,
        };

    private static EfCoreEnemyRepository CreateRepository(ContentDbContext db, IRandomSource? randomSource = null) =>
        new(db, NullLogger<EfCoreEnemyRepository>.Instance, randomSource);

    [Fact]
    public async Task GetBySlugAsync_CarriesTheAuthoredHealthOntoCurrentAndMaximumHealth()
    {
        await using var db = CreateDbContext();
        db.ActorArchetypes.Add(MakeArchetype("fire-ancient", new ArchetypeStats { Health = 900 }));
        await db.SaveChangesAsync();

        var enemy = await CreateRepository(db, new ScriptedRandomSource()).GetBySlugAsync("fire-ancient");

        enemy.Should().NotBeNull();
        enemy!.MaxHealth.Should().Be(900, "combat reads MaxHealth, and the archetype authored 900");
        enemy.Health.Should().Be(900, "a spawn arrives at full health");
    }

    [Fact]
    public async Task GetBySlugAsync_CarriesTheAuthoredDamageOntoBasePhysicalDamage()
    {
        await using var db = CreateDbContext();
        db.ActorArchetypes.Add(MakeArchetype("fire-ancient", new ArchetypeStats { Health = 900, Damage = 42 }));
        await db.SaveChangesAsync();

        var enemy = await CreateRepository(db).GetBySlugAsync("fire-ancient");

        enemy!.BasePhysicalDamage.Should().Be(42, "5 is the template default, not the authored damage");
    }

    [Fact]
    public async Task GetBySlugAsync_CarriesTheAuthoredExperienceOntoXp()
    {
        await using var db = CreateDbContext();
        db.ActorArchetypes.Add(MakeArchetype("fire-ancient", new ArchetypeStats { ExperienceReward = 1200 }));
        await db.SaveChangesAsync();

        var enemy = await CreateRepository(db).GetBySlugAsync("fire-ancient");

        enemy!.XP.Should().Be(1200, "25 is the template default, not the authored reward");
    }

    [Fact]
    public async Task GetBySlugAsync_CarriesTheAuthoredManaOntoCurrentAndMaximumMana()
    {
        await using var db = CreateDbContext();
        db.ActorArchetypes.Add(MakeArchetype("fire-ancient", new ArchetypeStats { Health = 900, Mana = 200 }));
        await db.SaveChangesAsync();

        var enemy = await CreateRepository(db).GetBySlugAsync("fire-ancient");

        enemy!.MaxMana.Should().Be(200);
        enemy.Mana.Should().Be(200, "a spawn arrives with its pool full");
    }

    [Fact]
    public async Task GetBySlugAsync_LeavesManaAtZeroWhenTheArchetypeAuthorsNone()
    {
        await using var db = CreateDbContext();
        db.ActorArchetypes.Add(MakeArchetype("giant-rat", new ArchetypeStats { Health = 20, Mana = null }));
        await db.SaveChangesAsync();

        var enemy = await CreateRepository(db).GetBySlugAsync("giant-rat");

        enemy!.MaxMana.Should().Be(0);
        enemy.Mana.Should().Be(0);
    }

    [Fact]
    public async Task GetBySlugAsync_CarriesTheAuthoredAttributesOntoTheTypedProperties()
    {
        await using var db = CreateDbContext();
        db.ActorArchetypes.Add(MakeArchetype("fire-ancient", new ArchetypeStats
        {
            Health = 900,
            Strength = 30,
            Agility = 12,
            Intelligence = 22,
            Constitution = 26,
        }));
        await db.SaveChangesAsync();

        var enemy = await CreateRepository(db).GetBySlugAsync("fire-ancient");

        // 10 is the template default for all four; the authored numbers have to replace it on the typed members.
        enemy!.Strength.Should().Be(30, "combat reads Enemy.Strength, not the archetype row");
        enemy.Dexterity.Should().Be(12, "the archetype's Agility is the template's Dexterity");
        enemy.Intelligence.Should().Be(22);
        enemy.Constitution.Should().Be(26);
    }

    [Fact]
    public async Task GetBySlugAsync_MirrorsTheTypedAttributesIntoTheDictionary()
    {
        await using var db = CreateDbContext();
        db.ActorArchetypes.Add(MakeArchetype("giant-rat", new ArchetypeStats
        {
            Health = 20,
            Strength = 4,
            Agility = 10,
            Intelligence = 2,
            Constitution = 6,
        }, minLevel: 1));
        await db.SaveChangesAsync();

        var enemy = await CreateRepository(db).GetBySlugAsync("giant-rat");

        // The dictionary is a mirror of the typed properties, so it has to equal them key by key — not merely be
        // populated — and keep the lowercase key names consumers already read.
        enemy!.Attributes["strength"].Should().Be(enemy.Strength).And.Be(4);
        enemy.Attributes["dexterity"].Should().Be(enemy.Dexterity).And.Be(10);
        enemy.Attributes["intelligence"].Should().Be(enemy.Intelligence).And.Be(2);
        enemy.Attributes["constitution"].Should().Be(enemy.Constitution).And.Be(6);
        enemy.Attributes.Should().HaveCount(4, "the four authored attributes are the whole attribute surface of a spawn");
    }

    [Fact]
    public async Task GetBySlugAsync_KeepsTheModelDefaultForAttributesTheArchetypeDoesNotAuthor()
    {
        await using var db = CreateDbContext();
        // village-blacksmith authors health and trade stats only — no attributes at all.
        db.ActorArchetypes.Add(MakeArchetype(
            "village-blacksmith",
            new ArchetypeStats { Health = 80, TradeSkill = 70, TradeGold = 500 },
            minLevel: 1));
        await db.SaveChangesAsync();

        var enemy = await CreateRepository(db).GetBySlugAsync("village-blacksmith");

        // 10 is the Enemy model's own default; the projection reads it from the model rather than restating it.
        enemy!.Strength.Should().Be(10);
        enemy.Dexterity.Should().Be(10);
        enemy.Intelligence.Should().Be(10);
        enemy.Constitution.Should().Be(10);
        enemy.Attributes["strength"].Should().Be(10);
        enemy.Attributes["dexterity"].Should().Be(10);
        enemy.Attributes["intelligence"].Should().Be(10);
        enemy.Attributes["constitution"].Should().Be(10);
    }

    [Fact]
    public async Task GetBySlugAsync_RollsGoldInclusivelyFromTheAuthoredRange()
    {
        await using var db = CreateDbContext();
        db.ActorArchetypes.Add(MakeArchetype("giant-rat", new ArchetypeStats { GoldRewardMin = 1, GoldRewardMax = 3 }));
        await db.SaveChangesAsync();

        // One draw per template, so scripting the two authored bounds pins both ends of the inclusive range.
        var atMinimum = await CreateRepository(db, ScriptedRandomSource.Returning(1)).GetBySlugAsync("giant-rat");
        var atMaximum = await CreateRepository(db, ScriptedRandomSource.Returning(3)).GetBySlugAsync("giant-rat");

        atMinimum!.GoldReward.Should().Be(1, "the authored minimum is reachable");
        atMaximum!.GoldReward.Should().Be(3, "the authored maximum is reachable, not excluded by an exclusive bound");
    }

    [Fact]
    public async Task GetBySlugAsync_RollsGoldOncePerTemplate()
    {
        await using var db = CreateDbContext();
        db.ActorArchetypes.Add(MakeArchetype("giant-rat", new ArchetypeStats { GoldRewardMin = 1, GoldRewardMax = 3 }));
        await db.SaveChangesAsync();
        var random = new ScriptedRandomSource(2);

        var enemy = await CreateRepository(db, random).GetBySlugAsync("giant-rat");

        enemy!.GoldReward.Should().Be(2);
        random.IntCallCount.Should().Be(1, "one spawn, one roll — a second draw is a second reward");
    }

    [Fact]
    public async Task GetBySlugAsync_RepeatsTheSameGoldUnderTheSameSeed()
    {
        await using var db = CreateDbContext();
        db.ActorArchetypes.Add(MakeArchetype("bandit-ruffian", new ArchetypeStats { GoldRewardMin = 5, GoldRewardMax = 15 }));
        await db.SaveChangesAsync();

        var first = await ReadGoldAsync(db, seed: 20260101, reads: 25);
        var second = await ReadGoldAsync(db, seed: 20260101, reads: 25);

        first.Should().Equal(second, "a fixed seed has to reproduce the whole sequence of rolls");
        first.Should().OnlyContain(gold => gold >= 5 && gold <= 15);
        first.Distinct().Should().HaveCountGreaterThan(1, "a seeded run still rolls, it does not pin one value");
    }

    [Fact]
    public async Task GetBySlugAsync_KeepsTheTemplateDefaultGoldWhenTheArchetypeAuthorsNoRange()
    {
        await using var db = CreateDbContext();
        db.ActorArchetypes.Add(MakeArchetype("wayfarer-elder", new ArchetypeStats { Health = 40 }));
        await db.SaveChangesAsync();
        var random = new ScriptedRandomSource();

        var enemy = await CreateRepository(db, random).GetBySlugAsync("wayfarer-elder");

        enemy!.GoldReward.Should().Be(10, "nothing was authored, so there is nothing to roll or invent");
        random.IntCallCount.Should().Be(0);
    }

    [Fact]
    public async Task GetBySlugAsync_CollapsesAHalfAuthoredGoldRangeOntoTheAuthoredBound()
    {
        await using var db = CreateDbContext();
        db.ActorArchetypes.Add(MakeArchetype("provisional-bandit", new ArchetypeStats { GoldRewardMax = 7 }));
        await db.SaveChangesAsync();
        var random = new ScriptedRandomSource();

        var enemy = await CreateRepository(db, random).GetBySlugAsync("provisional-bandit");

        enemy!.GoldReward.Should().Be(7, "a single authored number is a fixed reward, not a widened range");
        random.IntCallCount.Should().Be(0);
    }

    [Fact]
    public async Task GetAllAsync_CarriesTheAuthoredStatsOntoEveryTemplate()
    {
        await using var db = CreateDbContext();
        db.ActorArchetypes.AddRange(
            MakeArchetype("giant-rat", new ArchetypeStats { Health = 20, Damage = 3, ExperienceReward = 8, GoldRewardMin = 1, GoldRewardMax = 3 }, minLevel: 1),
            MakeArchetype("fire-ancient", new ArchetypeStats { Health = 900, Damage = 42, ExperienceReward = 1200, Mana = 200, GoldRewardMin = 150, GoldRewardMax = 300 }, minLevel: 26));
        await db.SaveChangesAsync();

        var enemies = await CreateRepository(db, ScriptedRandomSource.Seeded(7)).GetAllAsync();

        var rat = enemies.Single(e => e.Slug == "giant-rat");
        rat.MaxHealth.Should().Be(20);
        rat.Health.Should().Be(20);
        rat.BasePhysicalDamage.Should().Be(3);
        rat.XP.Should().Be(8);
        rat.GoldReward.Should().BeInRange(1, 3);

        var ancient = enemies.Single(e => e.Slug == "fire-ancient");
        ancient.MaxHealth.Should().Be(900);
        ancient.BasePhysicalDamage.Should().Be(42);
        ancient.XP.Should().Be(1200);
        ancient.MaxMana.Should().Be(200);
        ancient.GoldReward.Should().BeInRange(150, 300);
    }

    private static async Task<List<int>> ReadGoldAsync(ContentDbContext db, int seed, int reads)
    {
        var repository = CreateRepository(db, ScriptedRandomSource.Seeded(seed));
        var gold = new List<int>(reads);

        for (var i = 0; i < reads; i++)
        {
            var enemy = await repository.GetBySlugAsync("bandit-ruffian");
            gold.Add(enemy!.GoldReward);
        }

        return gold;
    }

    /// <summary>
    /// Integer-only <see cref="IRandomSource"/>: either a fixed script returned in order (a draw with nothing
    /// scripted throws, so a forgotten roll fails the test) or a fixed-seed <see cref="Random"/>.
    /// </summary>
    private sealed class ScriptedRandomSource : IRandomSource
    {
        private readonly Queue<int> _scripted;
        private readonly Random? _seeded;

        public ScriptedRandomSource(params int[] scripted) => _scripted = new Queue<int>(scripted);

        private ScriptedRandomSource(Random seeded)
        {
            _scripted = new Queue<int>();
            _seeded = seeded;
        }

        public static IRandomSource Returning(params int[] values) => new ScriptedRandomSource(values);

        public static IRandomSource Seeded(int seed) => new ScriptedRandomSource(new Random(seed));

        public int IntCallCount { get; private set; }

        public int Next(int maxValue) => Draw(0, maxValue);

        public int Next(int minValue, int maxValue) => Draw(minValue, maxValue);

        public double NextDouble() => throw new NotSupportedException("The archetype projection only draws integers.");

        private int Draw(int minValue, int maxValue)
        {
            IntCallCount++;

            if (_seeded is not null)
            {
                return _seeded.Next(minValue, maxValue);
            }

            if (_scripted.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No scripted draw left for Next({minValue}, {maxValue}); the projection rolled more than the test expected.");
            }

            var value = _scripted.Dequeue();
            if (value < minValue || value >= maxValue)
            {
                throw new InvalidOperationException(
                    $"Scripted {value} for Next({minValue}, {maxValue}), which is outside [{minValue}, {maxValue}).");
            }

            return value;
        }
    }
}
