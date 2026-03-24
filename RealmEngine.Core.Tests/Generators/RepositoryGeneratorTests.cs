using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Xunit;

namespace RealmEngine.Core.Tests.Generators;

[Trait("Category", "Generators")]
public class PowerGeneratorTests
{
    private readonly Mock<IPowerRepository> _repo = new();

    private PowerGenerator CreateGenerator() =>
        new(_repo.Object, NullLogger<PowerGenerator>.Instance);

    private static Power MakePower(string slug = "fireball") =>
        new() { Slug = slug, Name = slug, RarityWeight = 50 };

    [Fact]
    public async Task GenerateAbilitiesAsync_EmptyRepository_ReturnsEmpty()
    {
        _repo.Setup(r => r.GetByTypeAsync(It.IsAny<string>())).ReturnsAsync([]);
        var result = await CreateGenerator().GenerateAbilitiesAsync("active", "combat");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateAbilitiesAsync_WithPowers_ReturnsRequestedCount()
    {
        var powers = Enumerable.Range(0, 10).Select(i => MakePower($"power-{i}")).ToList();
        _repo.Setup(r => r.GetByTypeAsync(It.IsAny<string>())).ReturnsAsync(powers);
        var result = await CreateGenerator().GenerateAbilitiesAsync("active", "combat", count: 3);
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GenerateAbilitiesAsync_RepositoryThrows_ReturnsEmpty()
    {
        _repo.Setup(r => r.GetByTypeAsync(It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("DB fail"));
        var result = await CreateGenerator().GenerateAbilitiesAsync("active", "combat");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateAbilityByNameAsync_Found_ReturnsPower()
    {
        var power = MakePower("fireball");
        _repo.Setup(r => r.GetBySlugAsync("fireball")).ReturnsAsync(power);
        var result = await CreateGenerator().GenerateAbilityByNameAsync("active", "combat", "fireball");
        result.Should().NotBeNull();
        result!.Slug.Should().Be("fireball");
    }

    [Fact]
    public async Task GenerateAbilityByNameAsync_NotFound_ReturnsNull()
    {
        _repo.Setup(r => r.GetBySlugAsync(It.IsAny<string>())).ReturnsAsync((Power?)null);
        var result = await CreateGenerator().GenerateAbilityByNameAsync("active", "combat", "unknown");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GenerateAbilityByNameAsync_RepositoryThrows_ReturnsNull()
    {
        _repo.Setup(r => r.GetBySlugAsync(It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("DB fail"));
        var result = await CreateGenerator().GenerateAbilityByNameAsync("active", "combat", "fireball");
        result.Should().BeNull();
    }
}

[Trait("Category", "Generators")]
public class EnemyGeneratorTests
{
    private readonly Mock<IEnemyRepository> _repo = new();

    private EnemyGenerator CreateGenerator() =>
        new(_repo.Object, NullLogger<EnemyGenerator>.Instance);

    private static Enemy MakeEnemy(string slug = "goblin") =>
        new() { Slug = slug, Name = slug, RarityWeight = 50 };

    [Fact]
    public async Task GenerateEnemiesAsync_EmptyRepository_ReturnsEmpty()
    {
        _repo.Setup(r => r.GetByFamilyAsync(It.IsAny<string>())).ReturnsAsync([]);
        var result = await CreateGenerator().GenerateEnemiesAsync("goblins");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateEnemiesAsync_WithEnemies_ReturnsRequestedCount()
    {
        var enemies = Enumerable.Range(0, 5).Select(i => MakeEnemy($"enemy-{i}")).ToList();
        _repo.Setup(r => r.GetByFamilyAsync(It.IsAny<string>())).ReturnsAsync(enemies);
        var result = await CreateGenerator().GenerateEnemiesAsync("goblins", count: 3);
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GenerateEnemiesAsync_RepositoryThrows_ReturnsEmpty()
    {
        _repo.Setup(r => r.GetByFamilyAsync(It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("DB fail"));
        var result = await CreateGenerator().GenerateEnemiesAsync("goblins");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateEnemyByNameAsync_Found_ReturnsEnemy()
    {
        var enemy = MakeEnemy("goblin-scout");
        _repo.Setup(r => r.GetBySlugAsync("goblin-scout")).ReturnsAsync(enemy);
        var result = await CreateGenerator().GenerateEnemyByNameAsync("goblins", "goblin-scout");
        result.Should().NotBeNull();
        result!.Slug.Should().Be("goblin-scout");
    }

    [Fact]
    public async Task GenerateEnemyByNameAsync_NotFound_ReturnsNull()
    {
        _repo.Setup(r => r.GetBySlugAsync(It.IsAny<string>())).ReturnsAsync((Enemy?)null);
        var result = await CreateGenerator().GenerateEnemyByNameAsync("goblins", "unknown");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GenerateEnemyByNameAsync_RepositoryThrows_ReturnsNull()
    {
        _repo.Setup(r => r.GetBySlugAsync(It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("DB fail"));
        var result = await CreateGenerator().GenerateEnemyByNameAsync("goblins", "goblin-scout");
        result.Should().BeNull();
    }
}

[Trait("Category", "Generators")]
public class NpcGeneratorTests
{
    private readonly Mock<INpcRepository> _repo = new();

    private NpcGenerator CreateGenerator() =>
        new(_repo.Object, NullLogger<NpcGenerator>.Instance);

    private static NPC MakeNpc(string slug = "merchant") =>
        new() { Slug = slug, Name = slug };

    [Fact]
    public async Task GenerateNpcsAsync_EmptyRepository_ReturnsEmpty()
    {
        _repo.Setup(r => r.GetByCategoryAsync(It.IsAny<string>())).ReturnsAsync([]);
        var result = await CreateGenerator().GenerateNpcsAsync("merchants");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateNpcsAsync_WithNpcs_ReturnsRequestedCount()
    {
        var npcs = Enumerable.Range(0, 5).Select(i => MakeNpc($"npc-{i}")).ToList();
        _repo.Setup(r => r.GetByCategoryAsync(It.IsAny<string>())).ReturnsAsync(npcs);
        var result = await CreateGenerator().GenerateNpcsAsync("merchants", count: 3);
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GenerateNpcsAsync_RepositoryThrows_ReturnsEmpty()
    {
        _repo.Setup(r => r.GetByCategoryAsync(It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("DB fail"));
        var result = await CreateGenerator().GenerateNpcsAsync("merchants");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateNpcByNameAsync_Found_ReturnsNpc()
    {
        var npc = MakeNpc("blacksmith-joe");
        _repo.Setup(r => r.GetBySlugAsync("blacksmith-joe")).ReturnsAsync(npc);
        var result = await CreateGenerator().GenerateNpcByNameAsync("merchants", "blacksmith-joe");
        result.Should().NotBeNull();
        result!.Slug.Should().Be("blacksmith-joe");
    }

    [Fact]
    public async Task GenerateNpcByNameAsync_NotFound_ReturnsNull()
    {
        _repo.Setup(r => r.GetBySlugAsync(It.IsAny<string>())).ReturnsAsync((NPC?)null);
        var result = await CreateGenerator().GenerateNpcByNameAsync("merchants", "unknown");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GenerateNpcByNameAsync_RepositoryThrows_ReturnsNull()
    {
        _repo.Setup(r => r.GetBySlugAsync(It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("DB fail"));
        var result = await CreateGenerator().GenerateNpcByNameAsync("merchants", "blacksmith-joe");
        result.Should().BeNull();
    }
}

[Trait("Category", "Generators")]
public class QuestGeneratorTests
{
    private readonly Mock<IQuestRepository> _repo = new();

    private QuestGenerator CreateGenerator() =>
        new(_repo.Object, NullLogger<QuestGenerator>.Instance);

    private static Quest MakeQuest(string slug = "kill-goblins") =>
        new() { Slug = slug, RarityWeight = 50 };

    [Fact]
    public async Task GenerateQuestsAsync_EmptyRepository_ReturnsEmpty()
    {
        _repo.Setup(r => r.GetByTypeKeyAsync(It.IsAny<string>())).ReturnsAsync([]);
        var result = await CreateGenerator().GenerateQuestsAsync("side");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateQuestsAsync_WithQuests_ReturnsRequestedCount()
    {
        var quests = Enumerable.Range(0, 5).Select(i => MakeQuest($"quest-{i}")).ToList();
        _repo.Setup(r => r.GetByTypeKeyAsync(It.IsAny<string>())).ReturnsAsync(quests);
        var result = await CreateGenerator().GenerateQuestsAsync("side", count: 2);
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GenerateQuestsAsync_RepositoryThrows_ReturnsEmpty()
    {
        _repo.Setup(r => r.GetByTypeKeyAsync(It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("DB fail"));
        var result = await CreateGenerator().GenerateQuestsAsync("side");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateQuestByNameAsync_Found_ReturnsQuest()
    {
        var quest = MakeQuest("dragon-slayer");
        _repo.Setup(r => r.GetBySlugAsync("dragon-slayer")).ReturnsAsync(quest);
        var result = await CreateGenerator().GenerateQuestByNameAsync("main-story", "dragon-slayer");
        result.Should().NotBeNull();
        result!.Slug.Should().Be("dragon-slayer");
    }

    [Fact]
    public async Task GenerateQuestByNameAsync_NotFound_ReturnsNull()
    {
        _repo.Setup(r => r.GetBySlugAsync(It.IsAny<string>())).ReturnsAsync((Quest?)null);
        var result = await CreateGenerator().GenerateQuestByNameAsync("main-story", "unknown");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GenerateQuestByNameAsync_RepositoryThrows_ReturnsNull()
    {
        _repo.Setup(r => r.GetBySlugAsync(It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("DB fail"));
        var result = await CreateGenerator().GenerateQuestByNameAsync("main-story", "dragon-slayer");
        result.Should().BeNull();
    }
}

[Trait("Category", "Generators")]
public class CharacterClassGeneratorTests
{
    private readonly Mock<ICharacterClassRepository> _repo = new();

    private CharacterClassGenerator CreateGenerator() =>
        new(_repo.Object, NullLogger<CharacterClassGenerator>.Instance);

    private static CharacterClass MakeClass(string name) =>
        new() { Name = name, Slug = name.ToLowerInvariant() };

    [Fact]
    public async Task GetAllClassesAsync_WithClasses_ReturnsAll()
    {
        _repo.Setup(r => r.GetAll()).Returns([MakeClass("Warrior"), MakeClass("Mage")]);
        var result = await CreateGenerator().GetAllClassesAsync();
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllClassesAsync_EmptyRepository_ReturnsEmpty()
    {
        _repo.Setup(r => r.GetAll()).Returns([]);
        var result = await CreateGenerator().GetAllClassesAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllClassesAsync_RepositoryThrows_ReturnsEmpty()
    {
        _repo.Setup(r => r.GetAll()).Throws(new InvalidOperationException("DB fail"));
        var result = await CreateGenerator().GetAllClassesAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetClassByNameAsync_Found_ReturnsMatchingClass()
    {
        var cls = MakeClass("Rogue");
        _repo.Setup(r => r.GetByName("Rogue")).Returns(cls);
        var result = await CreateGenerator().GetClassByNameAsync("Rogue");
        result.Should().NotBeNull();
        result!.Name.Should().Be("Rogue");
    }

    [Fact]
    public async Task GetClassByNameAsync_NotFound_ReturnsNull()
    {
        _repo.Setup(r => r.GetByName(It.IsAny<string>())).Returns((CharacterClass?)null);
        var result = await CreateGenerator().GetClassByNameAsync("Unknown");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetClassByNameAsync_RepositoryThrows_ReturnsNull()
    {
        _repo.Setup(r => r.GetByName(It.IsAny<string>())).Throws(new InvalidOperationException("DB fail"));
        var result = await CreateGenerator().GetClassByNameAsync("Warrior");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetClassesByCategoryAsync_WithMatches_ReturnsSubset()
    {
        _repo.Setup(r => r.GetClassesByType("warrior")).Returns([MakeClass("Warrior"), MakeClass("Paladin")]);
        var result = await CreateGenerator().GetClassesByCategoryAsync("warrior");
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetClassesByCategoryAsync_RepositoryThrows_ReturnsEmpty()
    {
        _repo.Setup(r => r.GetClassesByType(It.IsAny<string>())).Throws(new InvalidOperationException("DB fail"));
        var result = await CreateGenerator().GetClassesByCategoryAsync("warrior");
        result.Should().BeEmpty();
    }
}
