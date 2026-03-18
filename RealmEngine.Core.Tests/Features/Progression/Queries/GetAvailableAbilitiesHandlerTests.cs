using Moq;
using RealmEngine.Core.Features.Progression.Queries;
using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Progression.Queries;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for GetAvailableAbilitiesHandler.
/// </summary>
public class GetAvailableAbilitiesHandlerTests
{
    private static async Task<GetAvailableAbilitiesHandler> CreateHandlerAsync(IEnumerable<Ability>? abilities = null)
    {
        var mockRepo = new Mock<IAbilityRepository>();
        mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync((abilities ?? []).ToList());
        var abilitySvc = new AbilityDataService(mockRepo.Object);
        await abilitySvc.InitializeAsync();
        return new GetAvailableAbilitiesHandler(abilitySvc);
    }

    private static Ability MakeAbility(string id, int requiredLevel = 1, string? className = null, int rarityWeight = 10) =>
        new()
        {
            Id = id,
            Slug = id,
            DisplayName = id,
            RequiredLevel = requiredLevel,
            AllowedClasses = className is null ? [] : [className],
            RarityWeight = rarityWeight
        };

    [Fact]
    public async Task Returns_Empty_WhenCatalogIsEmpty()
    {
        // Arrange
        var handler = await CreateHandlerAsync([]);
        var query = new GetAvailableAbilitiesQuery { ClassName = "Warrior", Level = 5 };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Abilities.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Returns_AllEligibleAbilities_ByLevelAndClass_WhenNoTierFilter()
    {
        // Arrange — 3 abilities, all at level 1 and unrestricted
        var handler = await CreateHandlerAsync([
            MakeAbility("slash", requiredLevel: 1),
            MakeAbility("shield-bash", requiredLevel: 1),
            MakeAbility("meteor", requiredLevel: 20)   // too high level
        ]);
        var query = new GetAvailableAbilitiesQuery { ClassName = "Warrior", Level = 5 };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert - only slash and shield-bash qualify (meteor requires level 20)
        result.Abilities.Should().HaveCount(2);
        result.Abilities.Select(a => a.Id).Should().BeEquivalentTo(["slash", "shield-bash"]);
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Excludes_Abilities_BelowRequiredLevel()
    {
        // Arrange
        var handler = await CreateHandlerAsync([
            MakeAbility("basic-attack", requiredLevel: 1),
            MakeAbility("power-strike", requiredLevel: 10),
            MakeAbility("berserker-rage", requiredLevel: 15)
        ]);
        var query = new GetAvailableAbilitiesQuery { ClassName = "Warrior", Level = 10 };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert - basic-attack (1) and power-strike (10) qualify; berserker-rage (15) does not
        result.Abilities.Should().HaveCount(2);
        result.Abilities.Select(a => a.Id).Should().BeEquivalentTo(["basic-attack", "power-strike"]);
    }

    [Fact]
    public async Task Returns_TierFiltered_WhenTierSpecified()
    {
        // Arrange — tier is calculated from RarityWeight: weight < 50 = tier 1
        var handler = await CreateHandlerAsync([
            MakeAbility("common-strike", rarityWeight: 10),     // tier 1 (weight < 50)
            MakeAbility("uncommon-blast", rarityWeight: 60),    // tier 2 (weight 50-99)
            MakeAbility("rare-burst", rarityWeight: 150)        // tier 3 (weight 100-199)
        ]);
        var query = new GetAvailableAbilitiesQuery { ClassName = "Mage", Level = 20, Tier = 1 };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert - only tier 1 ability returned
        result.Abilities.Should().HaveCount(1);
        result.Abilities[0].Id.Should().Be("common-strike");
    }

    [Fact]
    public async Task Returns_ClassRestricted_Abilities_ForCorrectClass()
    {
        // Arrange — smite is restricted to Paladin
        var handler = await CreateHandlerAsync([
            MakeAbility("smite", requiredLevel: 1, className: "Paladin"),
            MakeAbility("fireball", requiredLevel: 1)  // unrestricted
        ]);
        var query = new GetAvailableAbilitiesQuery { ClassName = "Paladin", Level = 5 };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert - Paladin gets both smite and fireball
        result.Abilities.Should().HaveCount(2);
        result.Abilities.Select(a => a.Id).Should().BeEquivalentTo(["smite", "fireball"]);
    }

    [Fact]
    public async Task Excludes_ClassRestricted_Abilities_ForWrongClass()
    {
        // Arrange
        var handler = await CreateHandlerAsync([
            MakeAbility("smite", requiredLevel: 1, className: "Paladin"),
            MakeAbility("fireball", requiredLevel: 1)  // unrestricted
        ]);
        var query = new GetAvailableAbilitiesQuery { ClassName = "Wizard", Level = 5 };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert - Wizard only gets fireball; smite is Paladin-only
        result.Abilities.Should().HaveCount(1);
        result.Abilities[0].Id.Should().Be("fireball");
    }

    [Fact]
    public async Task Returns_UnrestrictedAbilities_ForAllClasses()
    {
        // Arrange
        var handler = await CreateHandlerAsync([MakeAbility("basic-attack", requiredLevel: 1)]);

        foreach (var className in new[] { "Warrior", "Wizard", "Rogue", "Paladin", "Ranger" })
        {
            var query = new GetAvailableAbilitiesQuery { ClassName = className, Level = 1 };

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert - unrestricted ability available to everyone
            result.Abilities.Should().HaveCount(1, $"unrestricted ability should be available to {className}");
        }
    }
}
