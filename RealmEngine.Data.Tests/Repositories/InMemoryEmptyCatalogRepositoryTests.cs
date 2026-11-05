using RealmEngine.Data.Repositories;

namespace RealmEngine.Data.Tests.Repositories;

/// <summary>
/// Verifies the contract of the remaining "stub" InMemory repositories that exist so DI can resolve a catalog in
/// offline/test mode: every member returns an empty list or <see langword="null"/>, never a fabricated record.
/// </summary>
[Trait("Category", "Repository")]
public class InMemoryEmptyCatalogRepositoryTests
{
    [Fact]
    public async Task TraitDefinitionRepository_ReturnsNothing()
    {
        var repo = new InMemoryTraitDefinitionRepository();

        (await repo.GetAllAsync()).Should().BeEmpty();
        (await repo.GetByKeyAsync("keen-senses")).Should().BeNull();
        (await repo.GetByAppliesToAsync("enemy")).Should().BeEmpty();
    }

    [Fact]
    public async Task SpeciesRepository_ReturnsNothing()
    {
        var repo = new InMemorySpeciesRepository();

        (await repo.GetAllSpeciesAsync()).Should().BeEmpty();
        (await repo.GetSpeciesBySlugAsync("human")).Should().BeNull();
        (await repo.GetSpeciesByTypeAsync("humanoid")).Should().BeEmpty();
    }

    [Fact]
    public async Task OrganizationRepository_ReturnsNothing()
    {
        var repo = new InMemoryOrganizationRepository();

        (await repo.GetAllAsync()).Should().BeEmpty();
        (await repo.GetBySlugAsync("the-ashen-order")).Should().BeNull();
        (await repo.GetByTypeAsync("guild")).Should().BeEmpty();
    }

    [Fact]
    public async Task MaterialPropertyRepository_ReturnsNothing()
    {
        var repo = new InMemoryMaterialPropertyRepository();

        (await repo.GetAllAsync()).Should().BeEmpty();
        (await repo.GetBySlugAsync("iron")).Should().BeNull();
        (await repo.GetByFamilyAsync("metal")).Should().BeEmpty();
    }

    [Fact]
    public async Task ItemRepository_ReturnsNothing()
    {
        var repo = new InMemoryItemRepository();

        (await repo.GetAllAsync()).Should().BeEmpty();
        (await repo.GetBySlugAsync("iron-sword")).Should().BeNull();
        (await repo.GetByTypeAsync("weapons/swords")).Should().BeEmpty();
    }

    [Fact]
    public async Task EnchantmentRepository_ReturnsNothing()
    {
        var repo = new InMemoryEnchantmentRepository();

        (await repo.GetAllAsync()).Should().BeEmpty();
        (await repo.GetBySlugAsync("flaming")).Should().BeNull();
        (await repo.GetByTargetSlotAsync("MainHand")).Should().BeEmpty();
    }

    [Fact]
    public async Task DialogueRepository_ReturnsNothing()
    {
        var repo = new InMemoryDialogueRepository();

        (await repo.GetAllAsync()).Should().BeEmpty();
        (await repo.GetBySlugAsync("halda-greeting")).Should().BeNull();
        (await repo.GetBySpeakerAsync("innkeeper-halda")).Should().BeEmpty();
    }

    [Fact]
    public async Task ActorInstanceRepository_ReturnsNothing()
    {
        var repo = new InMemoryActorInstanceRepository();

        (await repo.GetAllAsync()).Should().BeEmpty();
        (await repo.GetBySlugAsync("giant-rat-of-the-cellar")).Should().BeNull();
        (await repo.GetByTypeKeyAsync("vermin")).Should().BeEmpty();
    }

    [Fact]
    public async Task PowerRepository_ReturnsNothingForASchoolQuery()
    {
        var repo = new InMemoryPowerRepository();

        (await repo.GetBySchoolAsync("fire")).Should().BeEmpty();
    }
}
