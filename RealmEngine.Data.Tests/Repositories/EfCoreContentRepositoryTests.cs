using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Repositories;

namespace RealmEngine.Data.Tests.Repositories;

[Trait("Category", "Repository")]
public class EfCoreAbilityRepositoryTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Ability MakeAbility(string slug, string type = "active", bool active = true) =>
        new() { Slug = slug, AbilityType = type, IsActive = active, DisplayName = slug };

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyActiveAbilities()
    {
        await using var db = CreateDbContext();
        db.Abilities.AddRange(
            MakeAbility("fireball", active: true),
            MakeAbility("hidden", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreAbilityRepository(db, NullLogger<EfCoreAbilityRepository>.Instance);

        var result = await repo.GetAllAsync();

        result.Should().HaveCount(1).And.Contain(a => a.Slug == "fireball");
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsMapping_WhenActive()
    {
        await using var db = CreateDbContext();
        db.Abilities.Add(MakeAbility("slash", "active"));
        await db.SaveChangesAsync();
        var repo = new EfCoreAbilityRepository(db, NullLogger<EfCoreAbilityRepository>.Instance);

        var result = await repo.GetBySlugAsync("slash");

        result.Should().NotBeNull();
        result!.Slug.Should().Be("slash");
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsNull_WhenInactive()
    {
        await using var db = CreateDbContext();
        db.Abilities.Add(MakeAbility("disabled", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreAbilityRepository(db, NullLogger<EfCoreAbilityRepository>.Instance);

        (await repo.GetBySlugAsync("disabled")).Should().BeNull();
    }

    [Fact]
    public async Task GetByTypeAsync_FiltersOnAbilityType()
    {
        await using var db = CreateDbContext();
        db.Abilities.AddRange(
            MakeAbility("strike", "active"),
            MakeAbility("regen", "passive"),
            MakeAbility("dodge", "active"));
        await db.SaveChangesAsync();
        var repo = new EfCoreAbilityRepository(db, NullLogger<EfCoreAbilityRepository>.Instance);

        var result = await repo.GetByTypeAsync("active");

        result.Should().HaveCount(2).And.OnlyContain(a => a.Slug != "regen");
    }
}

[Trait("Category", "Repository")]
public class EfCoreSkillRepositoryTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Skill MakeSkill(string slug, string typeKey = "combat", bool active = true) =>
        new() { Slug = slug, TypeKey = typeKey, IsActive = active, MaxRank = 5 };

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyActiveSkills()
    {
        await using var db = CreateDbContext();
        db.Skills.AddRange(MakeSkill("swords"), MakeSkill("hidden", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreSkillRepository(db, NullLogger<EfCoreSkillRepository>.Instance);

        (await repo.GetAllAsync()).Should().HaveCount(1);
    }

    [Fact]
    public async Task GetBySlugAsync_MapsFieldsCorrectly()
    {
        await using var db = CreateDbContext();
        db.Skills.Add(new Skill { Slug = "axes", TypeKey = "combat", IsActive = true, MaxRank = 10, DisplayName = "Axes" });
        await db.SaveChangesAsync();
        var repo = new EfCoreSkillRepository(db, NullLogger<EfCoreSkillRepository>.Instance);

        var skill = await repo.GetBySlugAsync("axes");

        skill.Should().NotBeNull();
        skill!.MaxRank.Should().Be(10);
        skill.Category.Should().Be("combat");
    }

    [Fact]
    public async Task GetByCategoryAsync_FiltersOnTypeKey()
    {
        await using var db = CreateDbContext();
        db.Skills.AddRange(
            MakeSkill("mining", "crafting"),
            MakeSkill("swords", "combat"),
            MakeSkill("alchemy", "crafting"));
        await db.SaveChangesAsync();
        var repo = new EfCoreSkillRepository(db, NullLogger<EfCoreSkillRepository>.Instance);

        var result = await repo.GetByCategoryAsync("crafting");

        result.Should().HaveCount(2).And.OnlyContain(s => s.Category == "crafting");
    }
}

[Trait("Category", "Repository")]
public class EfCoreBackgroundRepositoryTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Background MakeBackground(string slug, string typeKey = "strength", bool active = true) =>
        new() { Slug = slug, TypeKey = typeKey, IsActive = active, DisplayName = slug };

    [Fact]
    public async Task GetAllBackgroundsAsync_ReturnsOnlyActive()
    {
        await using var db = CreateDbContext();
        db.Backgrounds.AddRange(
            MakeBackground("soldier"),
            MakeBackground("hidden", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreBackgroundRepository(db, NullLogger<EfCoreBackgroundRepository>.Instance);

        (await repo.GetAllBackgroundsAsync()).Should().HaveCount(1);
    }

    [Fact]
    public async Task GetBackgroundByIdAsync_FindsByBareSlug()
    {
        await using var db = CreateDbContext();
        db.Backgrounds.Add(MakeBackground("merchant", "charisma"));
        await db.SaveChangesAsync();
        var repo = new EfCoreBackgroundRepository(db, NullLogger<EfCoreBackgroundRepository>.Instance);

        var result = await repo.GetBackgroundByIdAsync("merchant");

        result.Should().NotBeNull();
        result!.Slug.Should().Be("merchant");
    }

    [Fact]
    public async Task GetBackgroundByIdAsync_StripsPrefixedSlug()
    {
        await using var db = CreateDbContext();
        db.Backgrounds.Add(MakeBackground("farmer", "constitution"));
        await db.SaveChangesAsync();
        var repo = new EfCoreBackgroundRepository(db, NullLogger<EfCoreBackgroundRepository>.Instance);

        var result = await repo.GetBackgroundByIdAsync("backgrounds/constitution:farmer");

        result.Should().NotBeNull();
        result!.Slug.Should().Be("farmer");
    }

    [Fact]
    public async Task GetBackgroundsByAttributeAsync_FiltersOnTypeKey()
    {
        await using var db = CreateDbContext();
        db.Backgrounds.AddRange(
            MakeBackground("guard", "strength"),
            MakeBackground("merchant", "charisma"),
            MakeBackground("blacksmith", "strength"));
        await db.SaveChangesAsync();
        var repo = new EfCoreBackgroundRepository(db, NullLogger<EfCoreBackgroundRepository>.Instance);

        var result = await repo.GetBackgroundsByAttributeAsync("strength");

        result.Should().HaveCount(2).And.OnlyContain(b => b.PrimaryAttribute == "strength");
    }
}

[Trait("Category", "Repository")]
public class EfCoreRecipeRepositoryTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Recipe MakeRecipe(string slug, string skill = "blacksmithing", bool active = true) =>
        new()
        {
            Slug = slug,
            TypeKey = "weapons",
            CraftingSkill = skill,
            CraftingLevel = 1,
            OutputItemSlug = "iron-sword",
            OutputItemDomain = "weapons",
            IsActive = active
        };

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyActiveRecipes()
    {
        await using var db = CreateDbContext();
        db.Recipes.AddRange(MakeRecipe("iron-sword"), MakeRecipe("hidden", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreRecipeRepository(db, NullLogger<EfCoreRecipeRepository>.Instance);

        (await repo.GetAllAsync()).Should().HaveCount(1);
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsCorrectRecipe()
    {
        await using var db = CreateDbContext();
        db.Recipes.Add(MakeRecipe("steel-sword", "blacksmithing"));
        await db.SaveChangesAsync();
        var repo = new EfCoreRecipeRepository(db, NullLogger<EfCoreRecipeRepository>.Instance);

        var result = await repo.GetBySlugAsync("steel-sword");

        result.Should().NotBeNull();
        result!.Slug.Should().Be("steel-sword");
        result.RequiredSkill.Should().Be("blacksmithing");
    }

    [Fact]
    public async Task GetByCraftingSkillAsync_FiltersCorrectly()
    {
        await using var db = CreateDbContext();
        db.Recipes.AddRange(
            MakeRecipe("potion", "alchemy"),
            MakeRecipe("sword", "blacksmithing"),
            MakeRecipe("elixir", "alchemy"));
        await db.SaveChangesAsync();
        var repo = new EfCoreRecipeRepository(db, NullLogger<EfCoreRecipeRepository>.Instance);

        var result = await repo.GetByCraftingSkillAsync("alchemy");

        result.Should().HaveCount(2).And.OnlyContain(r => r.RequiredSkill == "alchemy");
    }
}
