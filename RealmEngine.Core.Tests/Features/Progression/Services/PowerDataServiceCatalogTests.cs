using Moq;
using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Progression.Services;

/// <summary>
/// Tests for <see cref="PowerDataService"/>'s catalog queries and tier maths — the parts the
/// handler suites never exercise (filters by type/effect/tradition/rank/tier, starting and
/// unlockable powers, learnable powers, repeat initialization and failure propagation).
/// </summary>
[Trait("Category", "Service")]
public class PowerDataServiceCatalogTests
{
    private static Power MakePower(
        string id,
        PowerType type = PowerType.Talent,
        MagicalTradition? tradition = null,
        PowerEffectType effectType = PowerEffectType.None,
        int rank = 0,
        int rarityWeight = 10,
        int requiredLevel = 1,
        int minimumSkillRank = 0,
        List<string>? allowedClasses = null) => new()
        {
            Id = id,
            Slug = id,
            Name = id,
            DisplayName = id,
            Description = id,
            Type = type,
            Tradition = tradition,
            EffectType = effectType,
            Rank = rank,
            RarityWeight = rarityWeight,
            RequiredLevel = requiredLevel,
            MinimumSkillRank = minimumSkillRank,
            AllowedClasses = allowedClasses ?? []
        };

    private static async Task<PowerDataService> CreateAsync(IEnumerable<Power>? powers = null)
    {
        var repository = new Mock<IPowerRepository>();
        repository.Setup(r => r.GetAllAsync()).ReturnsAsync((powers ?? []).ToList());
        var service = new PowerDataService(repository.Object);
        await service.InitializeAsync();
        return service;
    }

    private static List<Power> SampleCatalog() =>
    [
        MakePower("ability-open", PowerType.Talent, rank: 1),
        MakePower("ability-mage", PowerType.Talent, allowedClasses: ["Mage"], requiredLevel: 5),
        MakePower("talent-high", PowerType.Talent, rarityWeight: 60, requiredLevel: 10),
        MakePower("spell-fire", PowerType.Spell, MagicalTradition.Arcane, PowerEffectType.Damage, rarityWeight: 20),
        MakePower("cantrip-spark", PowerType.Cantrip, MagicalTradition.Divine, PowerEffectType.Buff),
        MakePower("prayer-heal", PowerType.Talent, MagicalTradition.Divine, PowerEffectType.Heal, minimumSkillRank: 25),
        MakePower("occult-hex", PowerType.Talent, MagicalTradition.Occult, PowerEffectType.Debuff),
        MakePower("primal-bite", PowerType.Talent, MagicalTradition.Primal, PowerEffectType.Damage)
    ];

    [Fact]
    public async Task GetAllPowers_ReturnsEveryInitializedPower()
    {
        var service = await CreateAsync(SampleCatalog());

        service.GetAllPowers().Should().HaveCount(8).And.ContainKey("spell-fire");
    }

    [Fact]
    public async Task GetPowersByType_FiltersByAcquisitionType()
    {
        var service = await CreateAsync(SampleCatalog());

        service.GetPowersByType(PowerType.Spell).Should().ContainSingle().Which.Id.Should().Be("spell-fire");
        service.GetPowersByType(PowerType.Cantrip).Should().ContainSingle().Which.Id.Should().Be("cantrip-spark");
    }

    [Fact]
    public async Task GetPowersByEffectType_FiltersByEffect()
    {
        var service = await CreateAsync(SampleCatalog());

        service.GetPowersByEffectType(PowerEffectType.Damage).Select(p => p.Id)
            .Should().BeEquivalentTo(["spell-fire", "primal-bite"]);
        service.GetPowersByEffectType(PowerEffectType.Heal).Should().ContainSingle().Which.Id.Should().Be("prayer-heal");
    }

    [Fact]
    public async Task GetPowersByTradition_ReturnsTraditionsPowers_AndEmptyForUnusedTradition()
    {
        var service = await CreateAsync(SampleCatalog());

        service.GetPowersByTradition(MagicalTradition.Divine).Select(p => p.Id)
            .Should().BeEquivalentTo(["cantrip-spark", "prayer-heal"]);
        service.GetPowersByTradition(MagicalTradition.Arcane).Should().ContainSingle().Which.Id.Should().Be("spell-fire");

        var empty = await CreateAsync();
        empty.GetPowersByTradition(MagicalTradition.Occult).Should().BeEmpty();
    }

    [Fact]
    public async Task GetPowersByRank_FiltersBySpellRank()
    {
        var service = await CreateAsync(SampleCatalog());

        service.GetPowersByRank(1).Should().ContainSingle().Which.Id.Should().Be("ability-open");
        service.GetPowersByRank(9).Should().BeEmpty();
    }

    [Fact]
    public async Task GetPowersByTier_GroupsPowersByRarityWeight()
    {
        var service = await CreateAsync(SampleCatalog());

        // rarityWeight < 50 => tier 1; 50..99 => tier 2
        service.GetPowersByTier(1).Select(p => p.Id).Should().Contain("spell-fire").And.Contain("ability-open");
        service.GetPowersByTier(2).Select(p => p.Id).Should().ContainSingle().Which.Should().Be("talent-high");
    }

    [Fact]
    public async Task GetStartingPowers_ExcludesSpellsAndCantrips_AndFiltersClass()
    {
        var service = await CreateAsync(SampleCatalog());

        var mage = service.GetStartingPowers("Mage");
        var rogue = service.GetStartingPowers("Rogue");

        mage.Select(p => p.Id).Should().Contain("ability-open").And.Contain("ability-mage");
        rogue.Select(p => p.Id).Should().Contain("ability-open").And.NotContain("ability-mage",
            "class-restricted powers are excluded for other classes");
        mage.Select(p => p.Id).Should().NotContain("spell-fire").And.NotContain("cantrip-spark")
            .And.NotContain("talent-high", "spells, cantrips and higher-tier powers are not starting powers");
    }

    [Fact]
    public async Task GetUnlockablePowers_FiltersByLevelAndClass()
    {
        var service = await CreateAsync(SampleCatalog());

        service.GetUnlockablePowers("Mage", 4).Select(p => p.Id)
            .Should().Contain("ability-open").And.NotContain("ability-mage");
        service.GetUnlockablePowers("Mage", 10).Select(p => p.Id)
            .Should().Contain("ability-mage").And.Contain("talent-high");
        service.GetUnlockablePowers("Rogue", 10).Select(p => p.Id)
            .Should().Contain("ability-open").And.NotContain("ability-mage");
    }

    [Fact]
    public async Task GetLearnablePowers_RequiresTraditionSkillAndSkillRankWithinTolerance()
    {
        var service = await CreateAsync(SampleCatalog());
        var character = new Character { Name = "Caster" };
        character.Skills["arcane"] = new CharacterSkill { SkillId = "arcane", CurrentRank = 5 };
        character.Skills["divine"] = new CharacterSkill { SkillId = "divine", CurrentRank = 5 };

        var learnable = service.GetLearnablePowers(character);

        learnable.Select(p => p.Id).Should().Contain("spell-fire", "minimum rank 0 is within tolerance")
            .And.Contain("cantrip-spark", "minimum rank 0 is within tolerance")
            .And.NotContain("prayer-heal", "minimum rank 25 is beyond rank 5 + 10 tolerance")
            .And.NotContain("occult-hex", "the character has no occult skill")
            .And.NotContain("ability-open", "powers without a tradition are never learnable from spellbooks");
    }

    [Fact]
    public async Task GetLearnablePowers_WithNoSkills_ReturnsEmpty()
    {
        var service = await CreateAsync(SampleCatalog());

        service.GetLearnablePowers(new Character { Name = "Blank" }).Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(49, 1)]
    [InlineData(50, 2)]
    [InlineData(99, 2)]
    [InlineData(100, 3)]
    [InlineData(199, 3)]
    [InlineData(200, 4)]
    [InlineData(399, 4)]
    [InlineData(400, 5)]
    public async Task CalculateTier_MapsRarityWeightToTier(int rarityWeight, int expectedTier)
    {
        var service = await CreateAsync([MakePower("power", rarityWeight: rarityWeight)]);

        service.CalculateTier(service.GetPower("power")!).Should().Be(expectedTier);
    }

    [Fact]
    public async Task CalculateTier_UltimatePathAlwaysTierFive()
    {
        var service = await CreateAsync([MakePower("powers/ultimate/doom", rarityWeight: 1)]);

        service.CalculateTier(service.GetPower("powers/ultimate/doom")!).Should().Be(5);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 10)]
    [InlineData(4, 15)]
    [InlineData(5, 20)]
    [InlineData(0, 1)]
    [InlineData(9, 1)]
    public void GetRequiredLevelForTier_MapsTierToLevel(int tier, int expectedLevel)
    {
        var service = new PowerDataService(Mock.Of<IPowerRepository>());

        service.GetRequiredLevelForTier(tier).Should().Be(expectedLevel);
    }

    [Fact]
    public async Task GetTraditionSkillId_MapsEachTradition()
    {
        var service = await CreateAsync();

        service.GetTraditionSkillId(MagicalTradition.Arcane).Should().Be("arcane");
        service.GetTraditionSkillId(MagicalTradition.Divine).Should().Be("divine");
        service.GetTraditionSkillId(MagicalTradition.Occult).Should().Be("occult");
        service.GetTraditionSkillId(MagicalTradition.Primal).Should().Be("primal");
        service.GetTraditionSkillId((MagicalTradition)99).Should().Be("arcane", "unknown traditions fall back to arcane");
    }

    [Fact]
    public async Task InitializeAsync_IsIdempotent_AndDoesNotDuplicatePowers()
    {
        var repository = new Mock<IPowerRepository>();
        repository.Setup(r => r.GetAllAsync()).ReturnsAsync([MakePower("only-power")]);
        var service = new PowerDataService(repository.Object);

        await service.InitializeAsync();
        await service.InitializeAsync();

        service.GetAllPowers().Should().ContainSingle();
        repository.Verify(r => r.GetAllAsync(), Times.Once(), "the second call must short-circuit on the initialized flag");
    }

    [Fact]
    public async Task InitializeAsync_PropagatesRepositoryFailure_AndLeavesServiceUninitialized()
    {
        var repository = new Mock<IPowerRepository>();
        repository.Setup(r => r.GetAllAsync()).ThrowsAsync(new InvalidOperationException("database unavailable"));
        var service = new PowerDataService(repository.Object);

        var act = () => service.InitializeAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("database unavailable");
        var read = () => service.GetAllPowers();
        read.Should().Throw<InvalidOperationException>().WithMessage("*not initialized*");
    }

    [Fact]
    public void GetPower_BeforeInitialization_Throws()
    {
        var service = new PowerDataService(Mock.Of<IPowerRepository>());

        var act = () => service.GetPower("anything");

        act.Should().Throw<InvalidOperationException>().WithMessage("*Call InitializeAsync() first*");
    }
}
