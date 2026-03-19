using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Services;

[Trait("Category", "Service")]
public class PassiveBonusCalculatorTests
{
    private static async Task<AbilityDataService> BuildAbilityDataService(IEnumerable<Ability> abilities)
    {
        var mockRepo = new Mock<IAbilityRepository>();
        mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(abilities.ToList());
        var svc = new AbilityDataService(mockRepo.Object);
        await svc.InitializeAsync();
        return svc;
    }

    private static Ability MakePassiveAbility(string id, string traitCategory) => new()
    {
        Id = id,
        DisplayName = id,
        IsPassive = true,
        Traits = new Dictionary<string, object>
        {
            ["category"] = new Dictionary<string, object> { ["value"] = traitCategory }
        }
    };

    private static Character CharacterWithAbility(string abilityId) => new()
    {
        Name = "Hero",
        LearnedAbilities = new Dictionary<string, CharacterAbility>
        {
            [abilityId] = new CharacterAbility { AbilityId = abilityId }
        }
    };

    [Fact]
    public async Task GetPhysicalDamageBonus_ReturnsZero_WhenCharacterHasNoAbilities()
    {
        var svc = await BuildAbilityDataService([]);
        var calculator = new PassiveBonusCalculator(svc);
        var character = new Character { Name = "Hero" };

        calculator.GetPhysicalDamageBonus(character).Should().Be(0);
    }

    [Fact]
    public async Task GetPhysicalDamageBonus_ReturnsZero_WhenAbilityIsNotPassive()
    {
        var activeAbility = new Ability
        {
            Id = "charge",
            DisplayName = "Charge",
            IsPassive = false,
            Traits = new Dictionary<string, object>
            {
                ["category"] = new Dictionary<string, object> { ["value"] = "offensive_traits" }
            }
        };
        var svc = await BuildAbilityDataService([activeAbility]);
        var calculator = new PassiveBonusCalculator(svc);

        calculator.GetPhysicalDamageBonus(CharacterWithAbility("charge")).Should().Be(0);
    }

    [Theory]
    [InlineData("offensive_traits")]
    [InlineData("combat_traits")]
    [InlineData("combat")]
    public async Task GetPhysicalDamageBonus_ReturnsBonus_ForCombatCategoryPassive(string category)
    {
        var ability = MakePassiveAbility("power-strike", category);
        var svc = await BuildAbilityDataService([ability]);
        var calculator = new PassiveBonusCalculator(svc);

        calculator.GetPhysicalDamageBonus(CharacterWithAbility("power-strike")).Should().Be(5);
    }

    [Fact]
    public async Task GetPhysicalDamageBonus_AccumulatesAcrossMultiplePassives()
    {
        var a1 = MakePassiveAbility("a1", "combat");
        var a2 = MakePassiveAbility("a2", "offensive_traits");
        var svc = await BuildAbilityDataService([a1, a2]);
        var calculator = new PassiveBonusCalculator(svc);
        var character = new Character
        {
            Name = "Hero",
            LearnedAbilities = new Dictionary<string, CharacterAbility>
            {
                ["a1"] = new CharacterAbility { AbilityId = "a1" },
                ["a2"] = new CharacterAbility { AbilityId = "a2" }
            }
        };

        calculator.GetPhysicalDamageBonus(character).Should().Be(10);
    }

    [Theory]
    [InlineData("magical")]
    [InlineData("elemental")]
    [InlineData("divine")]
    public async Task GetMagicDamageBonus_ReturnsBonus_ForMagicCategoryPassive(string category)
    {
        var ability = MakePassiveAbility("fireball-mastery", category);
        var svc = await BuildAbilityDataService([ability]);
        var calculator = new PassiveBonusCalculator(svc);

        calculator.GetMagicDamageBonus(CharacterWithAbility("fireball-mastery")).Should().Be(5);
    }

    [Fact]
    public async Task GetMagicDamageBonus_ReturnsZero_ForPhysicalCategoryPassive()
    {
        var ability = MakePassiveAbility("shield-mastery", "combat");
        var svc = await BuildAbilityDataService([ability]);
        var calculator = new PassiveBonusCalculator(svc);

        calculator.GetMagicDamageBonus(CharacterWithAbility("shield-mastery")).Should().Be(0);
    }

    [Theory]
    [InlineData("offensive_traits")]
    [InlineData("combat_traits")]
    public async Task GetCriticalChanceBonus_ReturnsBonus_ForOffensiveCategoryPassive(string category)
    {
        var ability = MakePassiveAbility("precision", category);
        var svc = await BuildAbilityDataService([ability]);
        var calculator = new PassiveBonusCalculator(svc);

        calculator.GetCriticalChanceBonus(CharacterWithAbility("precision")).Should().Be(2.0);
    }

    [Theory]
    [InlineData("defensive")]
    [InlineData("stealth")]
    public async Task GetDodgeChanceBonus_ReturnsBonus_ForDefensiveOrStealthPassive(string category)
    {
        var ability = MakePassiveAbility("evasion", category);
        var svc = await BuildAbilityDataService([ability]);
        var calculator = new PassiveBonusCalculator(svc);

        calculator.GetDodgeChanceBonus(CharacterWithAbility("evasion")).Should().Be(3.0);
    }

    [Theory]
    [InlineData("defensive")]
    [InlineData("combat")]
    public async Task GetDefenseBonus_ReturnsBonus_ForDefensiveCombatPassive(string category)
    {
        var ability = MakePassiveAbility("thick-skin", category);
        var svc = await BuildAbilityDataService([ability]);
        var calculator = new PassiveBonusCalculator(svc);

        calculator.GetDefenseBonus(CharacterWithAbility("thick-skin")).Should().Be(5);
    }

    [Fact]
    public async Task GetDefenseBonus_ReturnsZero_ForMagicPassive()
    {
        var ability = MakePassiveAbility("mana-shield", "magical");
        var svc = await BuildAbilityDataService([ability]);
        var calculator = new PassiveBonusCalculator(svc);

        calculator.GetDefenseBonus(CharacterWithAbility("mana-shield")).Should().Be(0);
    }
}
