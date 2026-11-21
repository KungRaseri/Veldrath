using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Repositories;
using RealmEngine.Data.Seeders;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Tests.Repositories;

/// <summary>
/// Guards the instance-composition contract: an <see cref="ActorInstance"/>'s overrides compose onto the archetype's
/// projected template (null = inherit, a number replaces absolutely), and a placed instance is what the spawn path
/// reads.
/// </summary>
/// <remarks>
/// These assertions are the acceptance test for A2. Before the projection composed overrides, every seeded instance
/// spawned as its archetype: <c>elder-goblin-chief</c> fought at goblin-scout damage, health and experience, and no
/// query returned a placed actor at all.
/// </remarks>
[Trait("Category", "Repository")]
public class ActorInstanceCompositionTests
{
    private static readonly Guid ScoutId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RuffianId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    /// <summary>The seeded <c>goblin-scout</c> archetype, mirrored from <c>ArchetypeSeeder</c>.</summary>
    private static ActorArchetype Scout(Guid? id = null) => new()
    {
        Id = id ?? ScoutId,
        Slug = "goblin-scout",
        TypeKey = "beasts",
        DisplayName = "Goblin Scout",
        IsActive = true,
        MinLevel = 1,
        MaxLevel = 3,
        RarityWeight = 80,
        Stats = new ArchetypeStats
        {
            Health = 30,
            Strength = 6,
            Agility = 12,
            Intelligence = 4,
            Constitution = 8,
            ArmorClass = 2,
            AttackBonus = 1,
            Damage = 4,
            ExperienceReward = 15,
            GoldRewardMin = 1,
            GoldRewardMax = 5,
        },
        Traits = new ArchetypeTraits { Hostile = true },
    };

    private static ActorArchetype Ruffian(Guid? id = null) => new()
    {
        Id = id ?? RuffianId,
        Slug = "bandit-ruffian",
        TypeKey = "humanoids",
        DisplayName = "Bandit Ruffian",
        IsActive = true,
        MinLevel = 2,
        MaxLevel = 6,
        RarityWeight = 60,
        Stats = new ArchetypeStats
        {
            Health = 45,
            Strength = 9,
            Agility = 7,
            Intelligence = 5,
            Constitution = 7,
            Damage = 7,
            ExperienceReward = 25,
            GoldRewardMin = 2,
            GoldRewardMax = 9,
        },
        Traits = new ArchetypeTraits { Hostile = true },
    };

    private static EfCoreActorInstanceRepository CreateRepository(ContentDbContext db, IRandomSource? random = null) =>
        new(db, NullLogger<EfCoreActorInstanceRepository>.Instance, random ?? new CountingRandomSource());

    /// <summary>The authored boss, with only the two fields this test overrides narrowed further.</summary>
    private static ActorInstance Chief() =>
        ActorInstancesSeeder.Authors(DateTimeOffset.UnixEpoch, ScoutId, RuffianId)
            .Single(instance => instance.Slug == "elder-goblin-chief");

    private static async Task<ContentDbContext> SeedChiefAsync(IRandomSource? random = null)
    {
        var db = CreateDbContext();
        db.ActorArchetypes.Add(Scout());
        db.ActorInstances.Add(Chief());
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task An_instance_overrides_only_the_fields_it_authors()
    {
        await using var db = await SeedChiefAsync();
        var authored = Chief();

        var placed = await CreateRepository(db).GetPlacedAsync("old-clearing");

        placed.Should().ContainSingle();
        var template = placed[0].Template;

        template.MaxHealth.Should().Be(authored.StatOverrides.Health);
        template.Health.Should().Be(authored.StatOverrides.Health);
        template.Strength.Should().Be(authored.StatOverrides.Strength);
        template.BasePhysicalDamage.Should().Be(authored.StatOverrides.Damage);
        template.XP.Should().Be(authored.StatOverrides.ExperienceReward);
        template.Level.Should().Be(authored.LevelOverride);

        // Everything the instance does not author is inherited from the archetype, not from the model defaults.
        template.Intelligence.Should().Be(4, "the archetype authors intelligence and the instance does not override it");
        template.Constitution.Should().Be(8, "constitution is inherited the same way");
        template.Dexterity.Should().Be(
            authored.StatOverrides.Agility,
            "Agility is projected onto Dexterity, and this instance authors it");
        template.MaxMana.Should().Be(0, "neither row authors mana, so the model default stands");
    }

    [Fact]
    public async Task A_fully_authored_instance_replaces_every_applied_field()
    {
        await using var db = CreateDbContext();
        db.ActorArchetypes.Add(Ruffian());
        db.ActorInstances.Add(ActorInstancesSeeder.Authors(DateTimeOffset.UnixEpoch, ScoutId, RuffianId)
            .Single(instance => instance.Slug == "captain-blackthorn"));
        await db.SaveChangesAsync();

        var placed = await CreateRepository(db).GetPlacedAsync("collapsed-vault");
        var authored = ActorInstancesSeeder.Authors(DateTimeOffset.UnixEpoch, ScoutId, RuffianId)
            .Single(instance => instance.Slug == "captain-blackthorn");

        placed.Should().ContainSingle();
        var template = placed[0].Template;
        var overrides = authored.StatOverrides;

        template.MaxHealth.Should().Be(overrides.Health);
        template.Strength.Should().Be(overrides.Strength);
        template.Dexterity.Should().Be(overrides.Agility);
        template.Intelligence.Should().Be(overrides.Intelligence);
        template.BasePhysicalDamage.Should().Be(overrides.Damage);
        template.XP.Should().Be(overrides.ExperienceReward);
        template.Level.Should().Be(8);
        placed[0].DisplayName.Should().Be("Captain Blackthorn");
    }

    [Fact]
    public async Task The_attributes_mirror_matches_the_typed_properties_after_an_override()
    {
        await using var db = await SeedChiefAsync();

        var template = (await CreateRepository(db).GetPlacedAsync("old-clearing"))[0].Template;

        template.Attributes["strength"].Should().Be(template.Strength);
        template.Attributes["dexterity"].Should().Be(template.Dexterity);
        template.Attributes["intelligence"].Should().Be(template.Intelligence);
        template.Attributes["constitution"].Should().Be(template.Constitution);
    }

    [Fact]
    public async Task An_instance_gold_override_rolls_inside_the_composed_range()
    {
        await using var db = await SeedChiefAsync();
        var authored = Chief().StatOverrides;
        var random = new CountingRandomSource();

        var template = (await CreateRepository(db, random).GetPlacedAsync("old-clearing"))[0].Template;

        random.Draws.Should().ContainSingle("one template spends exactly one gold draw");
        random.LastMin.Should().Be(authored.GoldRewardMin);
        random.LastMax.Should().Be(authored.GoldRewardMax + 1, "the authored maximum has to be reachable");
        template.GoldReward.Should().BeInRange(authored.GoldRewardMin!.Value, authored.GoldRewardMax!.Value);
    }

    [Fact]
    public async Task A_half_authored_gold_pair_collapses_onto_the_authored_bound_without_a_draw()
    {
        await using var db = CreateDbContext();
        db.ActorArchetypes.Add(Scout());
        db.ActorInstances.Add(new ActorInstance
        {
            Slug = "goblin-chief-min-only",
            TypeKey = "boss",
            DisplayName = "Goblin Chief",
            ArchetypeId = ScoutId,
            LocationSlug = "old-clearing",
            IsActive = true,
            StatOverrides = new InstanceStatOverrides { GoldRewardMin = 50 },
        });
        await db.SaveChangesAsync();
        var random = new CountingRandomSource();

        var template = (await CreateRepository(db, random).GetPlacedAsync("old-clearing"))[0].Template;

        // The composed pair is 50..5 — inverted, so the roll collapses onto the authored bound instead of throwing.
        template.GoldReward.Should().Be(50);
        random.Draws.Should().BeEmpty("a collapsed range has nothing to roll");
    }

    [Fact]
    public async Task An_instance_loot_override_replaces_the_archetype_table_and_null_inherits()
    {
        await using var db = CreateDbContext();
        var archetype = Scout();
        var bossTable = new LootTable { Slug = "goblin-boss-drops", TypeKey = "mobs", DisplayName = "Goblin Boss Drops" };
        var commonTable = new LootTable { Slug = "goblin-common-drops", TypeKey = "mobs", DisplayName = "Goblin Drops" };
        db.LootTables.AddRange(bossTable, commonTable);
        db.ActorArchetypes.Add(archetype);
        db.ActorInstances.AddRange(
            new ActorInstance
            {
                Slug = "boss-with-table",
                TypeKey = "boss",
                DisplayName = "Boss With Table",
                ArchetypeId = ScoutId,
                LootTableOverride = bossTable.Id,
                LocationSlug = "vault",
                IsActive = true,
            },
            new ActorInstance
            {
                Slug = "boss-inheriting",
                TypeKey = "boss",
                DisplayName = "Boss Inheriting",
                ArchetypeId = ScoutId,
                LocationSlug = "vault",
                IsActive = true,
            });
        archetype.LootTableId = commonTable.Id;
        await db.SaveChangesAsync();

        var placed = await CreateRepository(db).GetPlacedAsync("vault");
        var overridden = placed.Single(instance => instance.Slug == "boss-with-table");
        var inherited = placed.Single(instance => instance.Slug == "boss-inheriting");

        overridden.Template.LootTableIds.Should().Equal("goblin-boss-drops");
        inherited.Template.LootTableIds.Should().Equal("goblin-common-drops");
    }

    [Fact]
    public async Task Reserved_armor_and_attack_overrides_change_nothing()
    {
        typeof(Enemy).GetProperty("ArmorClass").Should().BeNull("the template has no armor-class member to carry it");
        typeof(Enemy).GetProperty("AttackBonus").Should().BeNull("no combat path rolls an attack for a bonus to modify");

        await using var db = CreateDbContext();
        var chief = Chief();
        chief.StatOverrides.ArmorClass = 99;
        chief.StatOverrides.AttackBonus = 99;
        db.ActorArchetypes.Add(Scout());
        db.ActorInstances.Add(chief);
        await db.SaveChangesAsync();

        var template = (await CreateRepository(db).GetPlacedAsync("old-clearing"))[0].Template;

        template.Strength.Should().Be(chief.StatOverrides.Strength);
        template.Constitution.Should().Be(8, "the reserved fields have no target, so nothing else moves");
        template.BasePhysicalDamage.Should().Be(chief.StatOverrides.Damage);
    }

    [Fact]
    public async Task GetPlacedAsync_Returns_Only_The_Active_Instance_Placed_There_Ordered_By_Slug()
    {
        await using var db = CreateDbContext();
        db.ActorArchetypes.AddRange(Scout(), Ruffian());
        db.ActorInstances.AddRange(
            new ActorInstance
            {
                Slug = "zulu-boss",
                TypeKey = "boss",
                DisplayName = "Zulu",
                ArchetypeId = ScoutId,
                LocationSlug = "old-clearing",
                IsActive = true,
            },
            new ActorInstance
            {
                Slug = "alpha-boss",
                TypeKey = "boss",
                DisplayName = "Alpha",
                ArchetypeId = ScoutId,
                LocationSlug = "old-clearing",
                IsActive = true,
            },
            new ActorInstance
            {
                Slug = "elsewhere-boss",
                TypeKey = "boss",
                DisplayName = "Elsewhere",
                ArchetypeId = ScoutId,
                LocationSlug = "collapsed-vault",
                IsActive = true,
            },
            new ActorInstance
            {
                Slug = "retired-boss",
                TypeKey = "boss",
                DisplayName = "Retired",
                ArchetypeId = ScoutId,
                LocationSlug = "old-clearing",
                IsActive = false,
            },
            new ActorInstance
            {
                Slug = "catalogue-only",
                TypeKey = "story",
                DisplayName = "Catalogued",
                ArchetypeId = ScoutId,
                IsActive = true,
            });
        await db.SaveChangesAsync();

        var placed = await CreateRepository(db).GetPlacedAsync("old-clearing");

        placed.Select(instance => instance.Slug).Should().Equal("alpha-boss", "zulu-boss");
        placed.Should().OnlyContain(instance => instance.LocationSlug == "old-clearing");

        var nowhere = await CreateRepository(db).GetPlacedAsync("a-location-nobody-stands-in");
        nowhere.Should().BeEmpty("a catalogue-only instance is never placed by a location query");
    }

    [Fact]
    public async Task GetPlacedAsync_Omits_An_Instance_Whose_Archetype_Is_Missing()
    {
        await using var db = CreateDbContext();
        db.ActorInstances.Add(new ActorInstance
        {
            Slug = "orphaned-boss",
            TypeKey = "boss",
            DisplayName = "Orphan",
            ArchetypeId = Guid.NewGuid(),
            LocationSlug = "old-clearing",
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var placed = await CreateRepository(db).GetPlacedAsync("old-clearing");

        placed.Should().BeEmpty("an instance with no archetype has no stats to compose onto");
    }

    [Fact]
    public async Task GetTemplateBySlugAsync_Composes_The_Override_For_The_Reload_Path()
    {
        await using var db = await SeedChiefAsync();
        var authored = Chief();

        var placed = await CreateRepository(db).GetTemplateBySlugAsync("elder-goblin-chief");

        placed.Should().NotBeNull();
        placed!.ArchetypeSlug.Should().Be("goblin-scout");
        placed.Slug.Should().Be("elder-goblin-chief");
        placed.Template.BasePhysicalDamage.Should().Be(
            authored.StatOverrides.Damage,
            "a reloaded boss must fight at its authored damage, not its archetype's");
        placed.Template.Strength.Should().Be(authored.StatOverrides.Strength);
    }

    /// <summary>Counting random source: one draw, recorded, so a test can price a projection.</summary>
    private sealed class CountingRandomSource : IRandomSource
    {
        /// <summary>Gets the values returned by every integer draw, in order.</summary>
        public List<int> Draws { get; } = [];

        /// <summary>Gets the inclusive lower bound of the last draw.</summary>
        public int LastMin { get; private set; }

        /// <summary>Gets the exclusive upper bound of the last draw.</summary>
        public int LastMax { get; private set; }

        /// <inheritdoc />
        public int Next(int maxValue) => Next(0, maxValue);

        /// <inheritdoc />
        public int Next(int minValue, int maxValue)
        {
            LastMin = minValue;
            LastMax = maxValue;
            var value = minValue;
            Draws.Add(value);
            return value;
        }

        /// <inheritdoc />
        public double NextDouble() => 0;
    }
}
