using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Repositories;
using RealmEngine.Shared.Models;
using Item = RealmEngine.Data.Entities.Item;
using Power = RealmEngine.Data.Entities.Power;

namespace RealmEngine.Data.Tests.Repositories;

/// <summary>
/// Tests for the catalog discriminator mappings the EF repositories apply when projecting content entities into
/// shared models: armour categories to <see cref="ItemType"/>, and power types/schools to
/// <see cref="PowerType"/> and <see cref="MagicalTradition"/>.
/// </summary>
[Trait("Category", "Repository")]
public class EfCoreCatalogMappingTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // ── Items ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("helmet", ItemType.Helmet)]
    [InlineData("helm", ItemType.Helmet)]
    [InlineData("head", ItemType.Helmet)]
    [InlineData("shoulder", ItemType.Shoulders)]
    [InlineData("chest", ItemType.Chest)]
    [InlineData("heavy", ItemType.Chest)]
    [InlineData("wrist", ItemType.Bracers)]
    [InlineData("hands", ItemType.Gloves)]
    [InlineData("belt", ItemType.Belt)]
    [InlineData("legs", ItemType.Legs)]
    [InlineData("boots", ItemType.Boots)]
    [InlineData("feet", ItemType.Boots)]
    [InlineData("trinket", ItemType.Chest)]
    public async Task GetBySlugAsync_MapsArmourCategoriesToItemTypes(string category, ItemType expected)
    {
        await using var db = CreateDbContext();
        db.Items.Add(new Item
        {
            Slug = "test-armour",
            DisplayName = "Test Armour",
            ItemType = "armor",
            TypeKey = category,
            IsActive = true,
        });
        await db.SaveChangesAsync();
        var repo = new EfCoreItemRepository(db, NullLogger<EfCoreItemRepository>.Instance);

        var result = await repo.GetBySlugAsync("test-armour");

        result.Should().NotBeNull();
        result!.Type.Should().Be(expected);
    }

    [Fact]
    public async Task GetBySlugAsync_MapsShieldsAndWeaponsAndConsumables()
    {
        await using var db = CreateDbContext();
        db.Items.AddRange(
            new Item { Slug = "buckler", DisplayName = "Buckler", ItemType = "armor", TypeKey = "shields", IsActive = true },
            new Item { Slug = "axe", DisplayName = "Axe", ItemType = "weapon", TypeKey = "axes", IsActive = true },
            new Item { Slug = "tonic", DisplayName = "Tonic", ItemType = "consumable", TypeKey = "potions", IsActive = true });
        await db.SaveChangesAsync();
        var repo = new EfCoreItemRepository(db, NullLogger<EfCoreItemRepository>.Instance);

        (await repo.GetBySlugAsync("buckler"))!.Type.Should().Be(ItemType.Shield);
        (await repo.GetBySlugAsync("axe"))!.Type.Should().Be(ItemType.Weapon);
        (await repo.GetBySlugAsync("tonic"))!.Type.Should().Be(ItemType.Consumable);
    }

    // ── Powers ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("cantrip", PowerType.Cantrip)]
    [InlineData("ultimate", PowerType.Ultimate)]
    [InlineData("reaction", PowerType.Reaction)]
    [InlineData("spell", PowerType.Spell)]
    [InlineData("mystery", PowerType.Talent)]
    public async Task GetBySlugAsync_MapsPowerTypes(string powerType, PowerType expected)
    {
        await using var db = CreateDbContext();
        db.Powers.Add(new Power
        {
            Slug = "test-power",
            DisplayName = "Test Power",
            PowerType = powerType,
            IsActive = true,
        });
        await db.SaveChangesAsync();
        var repo = new EfCorePowerRepository(db, NullLogger<EfCorePowerRepository>.Instance);

        var result = await repo.GetBySlugAsync("test-power");

        result.Should().NotBeNull();
        result!.Type.Should().Be(expected);
    }

    [Theory]
    [InlineData("holy", MagicalTradition.Divine)]
    [InlineData("sacred", MagicalTradition.Divine)]
    [InlineData("healing", MagicalTradition.Divine)]
    [InlineData("occult", MagicalTradition.Occult)]
    [InlineData("void", MagicalTradition.Occult)]
    [InlineData("psychic", MagicalTradition.Occult)]
    [InlineData("fire", MagicalTradition.Primal)]
    public async Task GetBySlugAsync_MapsSchoolsToTraditions(string school, MagicalTradition expected)
    {
        await using var db = CreateDbContext();
        db.Powers.Add(new Power
        {
            Slug = "test-power",
            DisplayName = "Test Power",
            PowerType = "spell",
            School = school,
            IsActive = true,
        });
        await db.SaveChangesAsync();
        var repo = new EfCorePowerRepository(db, NullLogger<EfCorePowerRepository>.Instance);

        var result = await repo.GetBySlugAsync("test-power");

        result!.Tradition.Should().Be(expected);
        result.School.Should().Be(school);
    }

    [Fact]
    public async Task GetBySchoolAsync_ReturnsOnlyActivePowersOfThatSchool()
    {
        await using var db = CreateDbContext();
        db.Powers.AddRange(
            new Power { Slug = "fireball", DisplayName = "Fireball", PowerType = "spell", School = "fire", IsActive = true },
            new Power { Slug = "frostbolt", DisplayName = "Frostbolt", PowerType = "spell", School = "frost", IsActive = true },
            new Power { Slug = "hellfire", DisplayName = "Hellfire", PowerType = "spell", School = "fire", IsActive = false });
        await db.SaveChangesAsync();
        var repo = new EfCorePowerRepository(db, NullLogger<EfCorePowerRepository>.Instance);

        var result = await repo.GetBySchoolAsync("fire");

        result.Should().ContainSingle().Which.Slug.Should().Be("fireball");
    }
}
