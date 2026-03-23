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

[Trait("Category", "Repository")]
public class EfCoreEquipmentSetRepositoryTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static EquipmentSetEntry MakeSet(string slug, string? displayName = null) =>
        new() { Slug = slug, DisplayName = displayName ?? slug, TypeKey = "equipment-set" };

    [Fact]
    public void GetAll_ReturnsAllRows()
    {
        using var db = CreateDbContext();
        db.EquipmentSets.AddRange(MakeSet("shadow-set"), MakeSet("iron-set"));
        db.SaveChanges();
        var repo = new EfCoreEquipmentSetRepository(db);

        repo.GetAll().Should().HaveCount(2);
    }

    [Fact]
    public void GetById_FindsBySlug()
    {
        using var db = CreateDbContext();
        db.EquipmentSets.Add(MakeSet("shadow-set", "Shadow Set"));
        db.SaveChanges();
        var repo = new EfCoreEquipmentSetRepository(db);

        var result = repo.GetById("shadow-set");

        result.Should().NotBeNull();
        result!.Name.Should().Be("Shadow Set");
    }

    [Fact]
    public void GetById_FindsByGuidString()
    {
        using var db = CreateDbContext();
        var entry = MakeSet("iron-set");
        db.EquipmentSets.Add(entry);
        db.SaveChanges();
        var repo = new EfCoreEquipmentSetRepository(db);

        var result = repo.GetById(entry.Id.ToString());

        result.Should().NotBeNull();
        result!.Id.Should().Be(entry.Id.ToString());
    }

    [Fact]
    public void GetById_ReturnsNull_WhenNotFound()
    {
        using var db = CreateDbContext();
        var repo = new EfCoreEquipmentSetRepository(db);

        repo.GetById("nonexistent").Should().BeNull();
    }

    [Fact]
    public void GetByName_FindsByDisplayName()
    {
        using var db = CreateDbContext();
        db.EquipmentSets.Add(MakeSet("forest-set", "Forest Set"));
        db.SaveChanges();
        var repo = new EfCoreEquipmentSetRepository(db);

        var result = repo.GetByName("Forest Set");

        result.Should().NotBeNull();
        result!.Name.Should().Be("Forest Set");
    }

    [Fact]
    public void GetByName_FindsBySlug()
    {
        using var db = CreateDbContext();
        db.EquipmentSets.Add(MakeSet("forest-set", "Forest Set"));
        db.SaveChanges();
        var repo = new EfCoreEquipmentSetRepository(db);

        var result = repo.GetByName("forest-set");

        result.Should().NotBeNull();
    }

    [Fact]
    public void GetByName_ReturnsNull_WhenNotFound()
    {
        using var db = CreateDbContext();
        var repo = new EfCoreEquipmentSetRepository(db);

        repo.GetByName("nonexistent").Should().BeNull();
    }
}

[Trait("Category", "Repository")]
public class EfCoreCharacterClassRepositoryTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ActorClass MakeClass(string slug, string typeKey = "fighters", string? displayName = null, bool active = true) =>
        new() { Slug = slug, TypeKey = typeKey, DisplayName = displayName ?? slug, IsActive = active, PrimaryStat = "strength" };

    [Fact]
    public void GetAll_ReturnsOnlyActiveClasses()
    {
        using var db = CreateDbContext();
        db.ActorClasses.AddRange(MakeClass("warrior"), MakeClass("hidden", active: false));
        db.SaveChanges();
        var repo = new EfCoreCharacterClassRepository(db, NullLogger<EfCoreCharacterClassRepository>.Instance);

        repo.GetAll().Should().HaveCount(1);
    }

    [Fact]
    public void GetBaseClasses_ReturnsAll_BecauseIsSubclassIsAlwaysFalse()
    {
        using var db = CreateDbContext();
        db.ActorClasses.AddRange(MakeClass("warrior"), MakeClass("mage", "casters"));
        db.SaveChanges();
        var repo = new EfCoreCharacterClassRepository(db, NullLogger<EfCoreCharacterClassRepository>.Instance);

        // MapToModel hardcodes IsSubclass = false, so all mapped classes are base classes
        repo.GetBaseClasses().Should().HaveCount(2);
        repo.GetSubclasses().Should().BeEmpty();
    }

    [Fact]
    public void GetClassesByType_FiltersById_WithTypeKeyPrefix()
    {
        using var db = CreateDbContext();
        db.ActorClasses.AddRange(
            MakeClass("warrior", "fighters", "Warrior"),
            MakeClass("mage", "casters", "Mage"));
        db.SaveChanges();
        var repo = new EfCoreCharacterClassRepository(db, NullLogger<EfCoreCharacterClassRepository>.Instance);

        // GetAll() maps Id = "{typeKey}:{displayName}" so filtering by typeKey prefix works
        var result = repo.GetClassesByType("fighters");

        result.Should().HaveCount(1).And.Contain(c => c.Slug == "warrior");
    }

    [Fact]
    public void GetByName_FindsByDisplayName_CaseInsensitive()
    {
        using var db = CreateDbContext();
        db.ActorClasses.Add(MakeClass("warrior", "fighters", "Warrior"));
        db.SaveChanges();
        var repo = new EfCoreCharacterClassRepository(db, NullLogger<EfCoreCharacterClassRepository>.Instance);

        var result = repo.GetByName("WARRIOR");

        result.Should().NotBeNull();
        result!.Slug.Should().Be("warrior");
    }

    [Fact]
    public void GetByName_ReturnsNull_WhenNotFound()
    {
        using var db = CreateDbContext();
        var repo = new EfCoreCharacterClassRepository(db, NullLogger<EfCoreCharacterClassRepository>.Instance);

        repo.GetByName("nonexistent").Should().BeNull();
    }

    [Fact]
    public void GetById_FindsByCompositeId()
    {
        using var db = CreateDbContext();
        db.ActorClasses.Add(MakeClass("warrior", "fighters", "Warrior"));
        db.SaveChanges();
        var repo = new EfCoreCharacterClassRepository(db, NullLogger<EfCoreCharacterClassRepository>.Instance);

        var result = repo.GetById("fighters:Warrior");

        result.Should().NotBeNull();
        result!.Slug.Should().Be("warrior");
    }

    [Fact]
    public void GetById_FallsBackToDisplayName_WhenIdNotFound()
    {
        using var db = CreateDbContext();
        db.ActorClasses.Add(MakeClass("warrior", "fighters", "Warrior"));
        db.SaveChanges();
        var repo = new EfCoreCharacterClassRepository(db, NullLogger<EfCoreCharacterClassRepository>.Instance);

        // Passing the display name directly uses the GetByName fallback
        var result = repo.GetById("Warrior");

        result.Should().NotBeNull();
        result!.Slug.Should().Be("warrior");
    }
}

[Trait("Category", "Repository")]
public class EfCoreSpeciesRepositoryTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static RealmEngine.Data.Entities.Species MakeSpecies(string slug, string typeKey = "humanoid", bool active = true) =>
        new() { Slug = slug, TypeKey = typeKey, DisplayName = slug, IsActive = active };

    [Fact]
    public async Task GetAllSpeciesAsync_ReturnsOnlyActive()
    {
        await using var db = CreateDbContext();
        db.Species.AddRange(MakeSpecies("human"), MakeSpecies("hidden", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreSpeciesRepository(db, NullLogger<EfCoreSpeciesRepository>.Instance);

        (await repo.GetAllSpeciesAsync()).Should().HaveCount(1);
    }

    [Fact]
    public async Task GetSpeciesBySlugAsync_ReturnsMappedModel()
    {
        await using var db = CreateDbContext();
        db.Species.Add(MakeSpecies("elf", "humanoid"));
        await db.SaveChangesAsync();
        var repo = new EfCoreSpeciesRepository(db, NullLogger<EfCoreSpeciesRepository>.Instance);

        var result = await repo.GetSpeciesBySlugAsync("elf");

        result.Should().NotBeNull();
        result!.Slug.Should().Be("elf");
        result.TypeKey.Should().Be("humanoid");
    }

    [Fact]
    public async Task GetSpeciesBySlugAsync_ReturnsNull_WhenInactive()
    {
        await using var db = CreateDbContext();
        db.Species.Add(MakeSpecies("ghost", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreSpeciesRepository(db, NullLogger<EfCoreSpeciesRepository>.Instance);

        (await repo.GetSpeciesBySlugAsync("ghost")).Should().BeNull();
    }

    [Fact]
    public async Task GetSpeciesByTypeAsync_FiltersOnTypeKey()
    {
        await using var db = CreateDbContext();
        db.Species.AddRange(
            MakeSpecies("human", "humanoid"),
            MakeSpecies("wolf",  "beast"),
            MakeSpecies("elf",   "humanoid"));
        await db.SaveChangesAsync();
        var repo = new EfCoreSpeciesRepository(db, NullLogger<EfCoreSpeciesRepository>.Instance);

        var result = await repo.GetSpeciesByTypeAsync("humanoid");

        result.Should().HaveCount(2).And.OnlyContain(s => s.TypeKey == "humanoid");
    }
}

[Trait("Category", "Repository")]
public class EfCoreMaterialRepositoryTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Material MakeMaterial(string slug, string family = "metal", bool active = true) =>
        new() { Slug = slug, TypeKey = family, MaterialFamily = family, IsActive = active, DisplayName = slug };

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyActiveMaterials()
    {
        await using var db = CreateDbContext();
        db.Materials.AddRange(MakeMaterial("iron"), MakeMaterial("hidden", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreMaterialRepository(db);

        (await repo.GetAllAsync()).Should().HaveCount(1);
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsMappedEntry()
    {
        await using var db = CreateDbContext();
        db.Materials.Add(MakeMaterial("iron", "metal"));
        await db.SaveChangesAsync();
        var repo = new EfCoreMaterialRepository(db);

        var result = await repo.GetBySlugAsync("iron");

        result.Should().NotBeNull();
        result!.Slug.Should().Be("iron");
        result.MaterialFamily.Should().Be("metal");
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsNull_WhenNotFound()
    {
        await using var db = CreateDbContext();
        var repo = new EfCoreMaterialRepository(db);

        (await repo.GetBySlugAsync("nonexistent")).Should().BeNull();
    }

    [Fact]
    public async Task GetByFamiliesAsync_FiltersOnMaterialFamily()
    {
        await using var db = CreateDbContext();
        db.Materials.AddRange(
            MakeMaterial("iron",     "metal"),
            MakeMaterial("oak",      "wood"),
            MakeMaterial("steel",    "metal"),
            MakeMaterial("leather",  "leather"));
        await db.SaveChangesAsync();
        var repo = new EfCoreMaterialRepository(db);

        var result = await repo.GetByFamiliesAsync(["metal", "wood"]);

        result.Should().HaveCount(3).And.NotContain(m => m.MaterialFamily == "leather");
    }
}

[Trait("Category", "Repository")]
public class EfCoreSpellRepositoryTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Spell MakeSpell(string slug, string school = "fire", bool active = true) =>
        new() { Slug = slug, TypeKey = school, School = school, IsActive = active, DisplayName = slug };

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyActiveSpells()
    {
        await using var db = CreateDbContext();
        db.Spells.AddRange(MakeSpell("fireball"), MakeSpell("hidden", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreSpellRepository(db, NullLogger<EfCoreSpellRepository>.Instance);

        (await repo.GetAllAsync()).Should().HaveCount(1);
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsMappedSpell()
    {
        await using var db = CreateDbContext();
        db.Spells.Add(MakeSpell("fireball", "fire"));
        await db.SaveChangesAsync();
        var repo = new EfCoreSpellRepository(db, NullLogger<EfCoreSpellRepository>.Instance);

        var result = await repo.GetBySlugAsync("fireball");

        result.Should().NotBeNull();
        result!.SpellId.Should().Be("fireball");
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsNull_WhenInactive()
    {
        await using var db = CreateDbContext();
        db.Spells.Add(MakeSpell("ghost-fire", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreSpellRepository(db, NullLogger<EfCoreSpellRepository>.Instance);

        (await repo.GetBySlugAsync("ghost-fire")).Should().BeNull();
    }

    [Fact]
    public async Task GetBySchoolAsync_FiltersOnSchool()
    {
        await using var db = CreateDbContext();
        db.Spells.AddRange(
            MakeSpell("fireball",   "fire"),
            MakeSpell("heal",       "healing"),
            MakeSpell("fire-storm", "fire"));
        await db.SaveChangesAsync();
        var repo = new EfCoreSpellRepository(db, NullLogger<EfCoreSpellRepository>.Instance);

        var result = await repo.GetBySchoolAsync("fire");

        result.Should().HaveCount(2).And.OnlyContain(s => s.SpellId != "heal");
    }
}

[Trait("Category", "Repository")]
public class EfCoreQuestRepositoryTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Quest MakeQuest(string slug, string typeKey = "main", bool active = true) =>
        new() { Slug = slug, TypeKey = typeKey, IsActive = active, DisplayName = slug, Traits = new() };

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyActiveQuests()
    {
        await using var db = CreateDbContext();
        db.Quests.AddRange(MakeQuest("rescue-mission"), MakeQuest("hidden", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreQuestRepository(db, NullLogger<EfCoreQuestRepository>.Instance);

        (await repo.GetAllAsync()).Should().HaveCount(1);
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsMappedQuest()
    {
        await using var db = CreateDbContext();
        db.Quests.Add(MakeQuest("rescue-mission", "main"));
        await db.SaveChangesAsync();
        var repo = new EfCoreQuestRepository(db, NullLogger<EfCoreQuestRepository>.Instance);

        var result = await repo.GetBySlugAsync("rescue-mission");

        result.Should().NotBeNull();
        result!.Slug.Should().Be("rescue-mission");
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsNull_WhenInactive()
    {
        await using var db = CreateDbContext();
        db.Quests.Add(MakeQuest("invisible-quest", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreQuestRepository(db, NullLogger<EfCoreQuestRepository>.Instance);

        (await repo.GetBySlugAsync("invisible-quest")).Should().BeNull();
    }

    [Fact]
    public async Task GetByTypeKeyAsync_FiltersOnTypeKey()
    {
        await using var db = CreateDbContext();
        db.Quests.AddRange(
            MakeQuest("quest-a", "main"),
            MakeQuest("quest-b", "side"),
            MakeQuest("quest-c", "main"));
        await db.SaveChangesAsync();
        var repo = new EfCoreQuestRepository(db, NullLogger<EfCoreQuestRepository>.Instance);

        var result = await repo.GetByTypeKeyAsync("main");

        result.Should().HaveCount(2).And.OnlyContain(q => q.Slug != "quest-b");
    }
}

[Trait("Category", "Repository")]
public class EfCoreNpcRepositoryTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ActorArchetype MakeArchetype(string slug, string typeKey = "merchant", bool active = true) =>
        new() { Slug = slug, TypeKey = typeKey, IsActive = active, DisplayName = slug };

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyActiveArchetypes()
    {
        await using var db = CreateDbContext();
        db.ActorArchetypes.AddRange(MakeArchetype("innkeeper"), MakeArchetype("hidden", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreNpcRepository(db, NullLogger<EfCoreNpcRepository>.Instance);

        (await repo.GetAllAsync()).Should().HaveCount(1);
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsMappedNpc()
    {
        await using var db = CreateDbContext();
        db.ActorArchetypes.Add(MakeArchetype("innkeeper", "innkeeper"));
        await db.SaveChangesAsync();
        var repo = new EfCoreNpcRepository(db, NullLogger<EfCoreNpcRepository>.Instance);

        var result = await repo.GetBySlugAsync("innkeeper");

        result.Should().NotBeNull();
        result!.Slug.Should().Be("innkeeper");
        result.Occupation.Should().Be("innkeeper");
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsNull_WhenInactive()
    {
        await using var db = CreateDbContext();
        db.ActorArchetypes.Add(MakeArchetype("ghost-npc", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreNpcRepository(db, NullLogger<EfCoreNpcRepository>.Instance);

        (await repo.GetBySlugAsync("ghost-npc")).Should().BeNull();
    }

    [Fact]
    public async Task GetByCategoryAsync_FiltersOnTypeKey()
    {
        await using var db = CreateDbContext();
        db.ActorArchetypes.AddRange(
            MakeArchetype("blacksmith", "merchant"),
            MakeArchetype("guard",      "guard"),
            MakeArchetype("alchemist",  "merchant"));
        await db.SaveChangesAsync();
        var repo = new EfCoreNpcRepository(db, NullLogger<EfCoreNpcRepository>.Instance);

        var result = await repo.GetByCategoryAsync("merchant");

        result.Should().HaveCount(2).And.OnlyContain(n => n.Occupation == "merchant");
    }
}

[Trait("Category", "Repository")]
public class EfCoreEnemyRepositoryTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ActorArchetype MakeArchetype(string slug, string typeKey = "beasts/wolf", bool active = true) =>
        new() { Slug = slug, TypeKey = typeKey, IsActive = active, DisplayName = slug };

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyActiveArchetypes()
    {
        await using var db = CreateDbContext();
        db.ActorArchetypes.AddRange(MakeArchetype("wolf"), MakeArchetype("hidden", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreEnemyRepository(db, NullLogger<EfCoreEnemyRepository>.Instance);

        (await repo.GetAllAsync()).Should().HaveCount(1);
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsMappedEnemy()
    {
        await using var db = CreateDbContext();
        db.ActorArchetypes.Add(MakeArchetype("wolf", "beasts/wolf"));
        await db.SaveChangesAsync();
        var repo = new EfCoreEnemyRepository(db, NullLogger<EfCoreEnemyRepository>.Instance);

        var result = await repo.GetBySlugAsync("wolf");

        result.Should().NotBeNull();
        result!.Slug.Should().Be("wolf");
        result.Attributes.Should().ContainKey("strength");
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsNull_WhenInactive()
    {
        await using var db = CreateDbContext();
        db.ActorArchetypes.Add(MakeArchetype("spectral-wolf", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreEnemyRepository(db, NullLogger<EfCoreEnemyRepository>.Instance);

        (await repo.GetBySlugAsync("spectral-wolf")).Should().BeNull();
    }

    [Fact]
    public async Task GetByFamilyAsync_FiltersOnTypeKey()
    {
        await using var db = CreateDbContext();
        db.ActorArchetypes.AddRange(
            MakeArchetype("wolf",   "beasts/wolf"),
            MakeArchetype("bandit", "humanoids/bandit"),
            MakeArchetype("bear",   "beasts/bear"));
        await db.SaveChangesAsync();
        var repo = new EfCoreEnemyRepository(db, NullLogger<EfCoreEnemyRepository>.Instance);

        var result = await repo.GetByFamilyAsync("beasts/wolf");

        result.Should().HaveCount(1).And.Contain(e => e.Slug == "wolf");
    }
}

[Trait("Category", "Repository")]
public class EfCoreLootTableRepositoryTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static LootTable MakeLootTable(string slug, string typeKey = "enemies", bool active = true) =>
        new() { Slug = slug, TypeKey = typeKey, IsActive = active, DisplayName = slug };

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyActiveLootTables()
    {
        await using var db = CreateDbContext();
        db.LootTables.AddRange(MakeLootTable("goblin-drops"), MakeLootTable("hidden", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreLootTableRepository(db, NullLogger<EfCoreLootTableRepository>.Instance);

        (await repo.GetAllAsync()).Should().HaveCount(1);
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsMappedLootTable()
    {
        await using var db = CreateDbContext();
        db.LootTables.Add(MakeLootTable("goblin-drops", "enemies"));
        await db.SaveChangesAsync();
        var repo = new EfCoreLootTableRepository(db, NullLogger<EfCoreLootTableRepository>.Instance);

        var result = await repo.GetBySlugAsync("goblin-drops");

        result.Should().NotBeNull();
        result!.Slug.Should().Be("goblin-drops");
        result.Context.Should().Be("enemies");
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsNull_WhenInactive()
    {
        await using var db = CreateDbContext();
        db.LootTables.Add(MakeLootTable("ghost-table", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreLootTableRepository(db, NullLogger<EfCoreLootTableRepository>.Instance);

        (await repo.GetBySlugAsync("ghost-table")).Should().BeNull();
    }

    [Fact]
    public async Task GetByContextAsync_FiltersOnTypeKey()
    {
        await using var db = CreateDbContext();
        db.LootTables.AddRange(
            MakeLootTable("orc-drops",  "enemies"),
            MakeLootTable("chest-a",    "chests"),
            MakeLootTable("wolf-drops", "enemies"));
        await db.SaveChangesAsync();
        var repo = new EfCoreLootTableRepository(db, NullLogger<EfCoreLootTableRepository>.Instance);

        var result = await repo.GetByContextAsync("enemies");

        result.Should().HaveCount(2).And.OnlyContain(t => t.Context == "enemies");
    }

    [Fact]
    public async Task GetBySlugAsync_IncludesEntries_WhenPresent()
    {
        await using var db = CreateDbContext();
        var table = MakeLootTable("boss-drops", "enemies");
        db.LootTables.Add(table);
        await db.SaveChangesAsync();
        db.LootTableEntries.Add(new LootTableEntry
        {
            LootTableId  = table.Id,
            ItemDomain   = "weapons",
            ItemSlug     = "iron-sword",
            DropWeight   = 50,
            QuantityMin  = 1,
            QuantityMax  = 1,
        });
        await db.SaveChangesAsync();
        var repo = new EfCoreLootTableRepository(db, NullLogger<EfCoreLootTableRepository>.Instance);

        var result = await repo.GetBySlugAsync("boss-drops");

        result.Should().NotBeNull();
        result!.Entries.Should().HaveCount(1);
        result.Entries[0].ItemSlug.Should().Be("iron-sword");
    }
}
