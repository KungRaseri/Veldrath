using RealmEngine.Data.Repositories;

namespace RealmEngine.Data.Tests.Repositories;

/// <summary>
/// Verifies the contract of every "stub" InMemory repository that returns
/// empty/null results by design (used for DI validation and offline testing).
/// </summary>
[Trait("Category", "Repository")]
public class InMemoryStubRepositoryTests
{
    // ── InMemoryQuestRepository ───────────────────────────────────────────

    [Fact]
    public async Task QuestRepository_GetAllAsync_ReturnsEmptyList()
    {
        var repo = new InMemoryQuestRepository();
        (await repo.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task QuestRepository_GetBySlugAsync_ReturnsNull()
    {
        var repo = new InMemoryQuestRepository();
        (await repo.GetBySlugAsync("any-slug")).Should().BeNull();
    }

    [Fact]
    public async Task QuestRepository_GetByTypeKeyAsync_ReturnsEmptyList()
    {
        var repo = new InMemoryQuestRepository();
        (await repo.GetByTypeKeyAsync("main")).Should().BeEmpty();
    }

    // ── InMemoryRecipeRepository ──────────────────────────────────────────

    [Fact]
    public async Task RecipeRepository_GetAllAsync_ReturnsEmptyList()
    {
        var repo = new InMemoryRecipeRepository();
        (await repo.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task RecipeRepository_GetBySlugAsync_ReturnsNull()
    {
        var repo = new InMemoryRecipeRepository();
        (await repo.GetBySlugAsync("any-slug")).Should().BeNull();
    }

    [Fact]
    public async Task RecipeRepository_GetByCraftingSkillAsync_ReturnsEmptyList()
    {
        var repo = new InMemoryRecipeRepository();
        (await repo.GetByCraftingSkillAsync("blacksmithing")).Should().BeEmpty();
    }

    // ── InMemoryPowerRepository ───────────────────────────────────────────

    [Fact]
    public async Task PowerRepository_GetAllAsync_ReturnsEmptyList()
    {
        var repo = new InMemoryPowerRepository();
        (await repo.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task PowerRepository_GetBySlugAsync_ReturnsNull()
    {
        var repo = new InMemoryPowerRepository();
        (await repo.GetBySlugAsync("power-strike")).Should().BeNull();
    }

    [Fact]
    public async Task PowerRepository_GetByTypeAsync_ReturnsEmptyList()
    {
        var repo = new InMemoryPowerRepository();
        (await repo.GetByTypeAsync("active")).Should().BeEmpty();
    }

    // ── InMemorySkillRepository ───────────────────────────────────────────

    [Fact]
    public async Task SkillRepository_GetAllAsync_ReturnsEmptyList()
    {
        var repo = new InMemorySkillRepository();
        (await repo.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task SkillRepository_GetBySlugAsync_ReturnsNull()
    {
        var repo = new InMemorySkillRepository();
        (await repo.GetBySlugAsync("blacksmithing")).Should().BeNull();
    }

    [Fact]
    public async Task SkillRepository_GetByCategoryAsync_ReturnsEmptyList()
    {
        var repo = new InMemorySkillRepository();
        (await repo.GetByCategoryAsync("crafting")).Should().BeEmpty();
    }

    // ── InMemorySpellRepository removed — spells unified into InMemoryPowerRepository ──

    // ── InMemoryBackgroundRepository ──────────────────────────────────────

    [Fact]
    public async Task BackgroundRepository_GetAllBackgroundsAsync_ReturnsEmptyList()
    {
        var repo = new InMemoryBackgroundRepository();
        (await repo.GetAllBackgroundsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task BackgroundRepository_GetBackgroundByIdAsync_ReturnsNull()
    {
        var repo = new InMemoryBackgroundRepository();
        (await repo.GetBackgroundByIdAsync("soldier")).Should().BeNull();
    }

    [Fact]
    public async Task BackgroundRepository_GetBackgroundsByAttributeAsync_ReturnsEmptyList()
    {
        var repo = new InMemoryBackgroundRepository();
        (await repo.GetBackgroundsByAttributeAsync("strength")).Should().BeEmpty();
    }

    // ── InMemoryCharacterClassRepository ─────────────────────────────────

    [Fact]
    public void CharacterClassRepository_GetAll_ReturnsEmptyList()
    {
        var repo = new InMemoryCharacterClassRepository();
        repo.GetAll().Should().BeEmpty();
    }

    [Fact]
    public void CharacterClassRepository_GetById_ReturnsNull()
    {
        var repo = new InMemoryCharacterClassRepository();
        repo.GetById("warrior").Should().BeNull();
    }

    [Fact]
    public void CharacterClassRepository_GetByName_ReturnsNull()
    {
        var repo = new InMemoryCharacterClassRepository();
        repo.GetByName("Warrior").Should().BeNull();
    }

    [Fact]
    public void CharacterClassRepository_GetClassesByType_ReturnsEmptyList()
    {
        var repo = new InMemoryCharacterClassRepository();
        repo.GetClassesByType("melee").Should().BeEmpty();
    }

    [Fact]
    public void CharacterClassRepository_GetBaseClasses_ReturnsEmptyList()
    {
        var repo = new InMemoryCharacterClassRepository();
        repo.GetBaseClasses().Should().BeEmpty();
    }

    [Fact]
    public void CharacterClassRepository_GetSubclasses_ReturnsEmptyList()
    {
        var repo = new InMemoryCharacterClassRepository();
        repo.GetSubclasses().Should().BeEmpty();
    }

    [Fact]
    public void CharacterClassRepository_GetSubclassesForParent_ReturnsEmptyList()
    {
        var repo = new InMemoryCharacterClassRepository();
        repo.GetSubclassesForParent("warrior").Should().BeEmpty();
    }

    // ── InMemoryEnemyRepository ───────────────────────────────────────────

    [Fact]
    public async Task EnemyRepository_GetAllAsync_ReturnsEmptyList()
    {
        var repo = new InMemoryEnemyRepository();
        (await repo.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task EnemyRepository_GetBySlugAsync_ReturnsNull()
    {
        var repo = new InMemoryEnemyRepository();
        (await repo.GetBySlugAsync("goblin")).Should().BeNull();
    }

    [Fact]
    public async Task EnemyRepository_GetByFamilyAsync_ReturnsEmptyList()
    {
        var repo = new InMemoryEnemyRepository();
        (await repo.GetByFamilyAsync("humanoid")).Should().BeEmpty();
    }

    // ── InMemoryNpcRepository ────────────────────────────────────────────

    [Fact]
    public async Task NpcRepository_GetAllAsync_ReturnsEmptyList()
    {
        var repo = new InMemoryNpcRepository();
        (await repo.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task NpcRepository_GetBySlugAsync_ReturnsNull()
    {
        var repo = new InMemoryNpcRepository();
        (await repo.GetBySlugAsync("innkeeper")).Should().BeNull();
    }

    [Fact]
    public async Task NpcRepository_GetByCategoryAsync_ReturnsEmptyList()
    {
        var repo = new InMemoryNpcRepository();
        (await repo.GetByCategoryAsync("merchant")).Should().BeEmpty();
    }

    // ── InMemoryLootTableRepository ───────────────────────────────────────

    [Fact]
    public async Task LootTableRepository_GetAllAsync_ReturnsEmptyList()
    {
        var repo = new InMemoryLootTableRepository();
        (await repo.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task LootTableRepository_GetBySlugAsync_ReturnsNull()
    {
        var repo = new InMemoryLootTableRepository();
        (await repo.GetBySlugAsync("goblin-drops")).Should().BeNull();
    }

    [Fact]
    public async Task LootTableRepository_GetByContextAsync_ReturnsEmptyList()
    {
        var repo = new InMemoryLootTableRepository();
        (await repo.GetByContextAsync("dungeon")).Should().BeEmpty();
    }

    // ── InMemoryMaterialRepository ────────────────────────────────────────

    [Fact]
    public async Task MaterialRepository_GetAllAsync_ReturnsEmptyList()
    {
        var repo = new InMemoryMaterialRepository();
        (await repo.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task MaterialRepository_GetByFamiliesAsync_ReturnsEmptyList()
    {
        var repo = new InMemoryMaterialRepository();
        (await repo.GetByFamiliesAsync(["metal", "wood"])).Should().BeEmpty();
    }

    [Fact]
    public async Task MaterialRepository_GetBySlugAsync_ReturnsNull()
    {
        var repo = new InMemoryMaterialRepository();
        (await repo.GetBySlugAsync("iron-ingot")).Should().BeNull();
    }

    // ── InMemoryEquipmentSetRepository (hardcoded data) ───────────────────

    [Fact]
    public void EquipmentSetRepository_GetAllSets_ReturnsFiveSets()
    {
        var repo = new InMemoryEquipmentSetRepository();
        repo.GetAllSets().Should().HaveCount(5);
    }

    [Fact]
    public void EquipmentSetRepository_GetAllSets_ContainsExpectedSetNames()
    {
        var repo = new InMemoryEquipmentSetRepository();
        var names = repo.GetAllSets().Select(s => s.Name).ToList();
        names.Should().Contain("Dragonborn")
             .And.Contain("Shadow Assassin")
             .And.Contain("Arcane Scholar");
    }
}
