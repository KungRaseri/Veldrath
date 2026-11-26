using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Seeders;

namespace RealmEngine.Data.Tests.Seeders;

/// <summary>
/// Guards <see cref="ActorInstancesSeeder"/>: the authored rows land placed, a restart does not duplicate them, and the
/// narrow placement backfill reaches an older row without overwriting anything an author has edited.
/// </summary>
[Trait("Category", "Seed")]
public class ActorInstancesSeederTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<ContentDbContext> SeedArchetypesAsync()
    {
        var db = CreateDbContext();
        db.ActorArchetypes.AddRange(
            new ActorArchetype
            {
                Slug = "goblin-scout",
                TypeKey = "beasts",
                DisplayName = "Goblin Scout",
                IsActive = true,
                MinLevel = 1,
                MaxLevel = 3,
            },
            new ActorArchetype
            {
                Slug = "bandit-ruffian",
                TypeKey = "humanoids",
                DisplayName = "Bandit Ruffian",
                IsActive = true,
                MinLevel = 2,
                MaxLevel = 6,
            });
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Seeding_places_the_two_authored_instances()
    {
        await using var db = await SeedArchetypesAsync();

        await ActorInstancesSeeder.SeedAsync(db);

        var rows = await db.ActorInstances.AsNoTracking().ToListAsync();
        rows.Should().HaveCount(2);
        rows.Single(row => row.Slug == "elder-goblin-chief").LocationSlug.Should().Be("old-clearing");
        rows.Single(row => row.Slug == "captain-blackthorn").LocationSlug.Should().Be("collapsed-vault");
    }

    [Fact]
    public async Task The_authored_rows_carry_no_reserved_stat_overrides()
    {
        await using var db = await SeedArchetypesAsync();

        await ActorInstancesSeeder.SeedAsync(db);

        var rows = await db.ActorInstances.AsNoTracking().ToListAsync();
        rows.Should().OnlyContain(row => row.StatOverrides.ArmorClass == null && row.StatOverrides.AttackBonus == null);
    }

    [Fact]
    public async Task Seeding_twice_is_idempotent()
    {
        await using var db = await SeedArchetypesAsync();

        await ActorInstancesSeeder.SeedAsync(db);
        await ActorInstancesSeeder.SeedAsync(db);

        (await db.ActorInstances.AsNoTracking().CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task A_stored_row_with_no_location_gains_the_authored_one()
    {
        await using var db = await SeedArchetypesAsync();
        var archetypeId = await db.ActorArchetypes
            .Where(archetype => archetype.Slug == "goblin-scout")
            .Select(archetype => archetype.Id)
            .SingleAsync();

        // The row an older database already holds: authored, but unplaced, because the column did not exist then.
        db.ActorInstances.Add(new ActorInstance
        {
            Slug = "elder-goblin-chief",
            TypeKey = "boss",
            DisplayName = "Elder Goblin Chief",
            ArchetypeId = archetypeId,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        await ActorInstancesSeeder.SeedAsync(db);

        var stored = await db.ActorInstances.AsNoTracking().SingleAsync(row => row.Slug == "elder-goblin-chief");
        stored.LocationSlug.Should().Be("old-clearing");
        (await db.ActorInstances.AsNoTracking().CountAsync()).Should().Be(2, "the other authored row is still inserted");
    }

    [Fact]
    public async Task A_hand_edited_display_name_and_placement_survive_a_second_seed()
    {
        await using var db = await SeedArchetypesAsync();
        await ActorInstancesSeeder.SeedAsync(db);

        var stored = await db.ActorInstances.SingleAsync(row => row.Slug == "elder-goblin-chief");
        stored.DisplayName = "The Old Clearing Chief";
        stored.LocationSlug = "wolf-hollow";
        await db.SaveChangesAsync();

        await ActorInstancesSeeder.SeedAsync(db);

        var reloaded = await db.ActorInstances.AsNoTracking().SingleAsync(row => row.Slug == "elder-goblin-chief");
        reloaded.DisplayName.Should().Be("The Old Clearing Chief", "the seed is not a reconcile");
        reloaded.LocationSlug.Should().Be("wolf-hollow", "a row that already has a location is never re-pointed");
    }

    [Fact]
    public void The_authored_values_are_what_the_seed_promises()
    {
        var authored = ActorInstancesSeeder.Authors(
            DateTimeOffset.UnixEpoch,
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"));

        authored.Select(row => row.Slug).Should().OnlyHaveUniqueItems();
        authored.Should().OnlyContain(row => row.LocationSlug != null);
        authored.Should().Contain(row => row.Slug == "elder-goblin-chief" && row.LocationSlug == "old-clearing");
        authored.Should().Contain(row => row.Slug == "captain-blackthorn" && row.LocationSlug == "collapsed-vault");
    }
}
