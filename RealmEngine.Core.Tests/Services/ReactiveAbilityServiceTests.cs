using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Services;

[Trait("Category", "Service")]
public class ReactiveAbilityServiceTests
{
    private static async Task<AbilityDataService> BuildAbilityDataService(IEnumerable<Ability> abilities)
    {
        var mockRepo = new Mock<IAbilityRepository>();
        mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(abilities.ToList());
        var svc = new AbilityDataService(mockRepo.Object);
        await svc.InitializeAsync();
        return svc;
    }

    /// <summary>
    /// Creates a reactive ability with string-format traits (the simpler path in IsReactiveAbility).
    /// </summary>
    private static Ability MakeReactiveAbility(string id, string triggerCondition, int cooldown = 0) => new()
    {
        Id = id,
        DisplayName = id,
        IsPassive = false,
        Cooldown = cooldown,
        Traits = new Dictionary<string, object>
        {
            ["abilityClass"] = "reactive",
            ["triggerCondition"] = triggerCondition
        }
    };

    private static Character CharacterWithAbility(string abilityId, int cooldownRemaining = 0)
    {
        var character = new Character { Name = "Hero" };
        character.LearnedAbilities[abilityId] = new CharacterAbility { AbilityId = abilityId };
        if (cooldownRemaining > 0)
            character.AbilityCooldowns[abilityId] = cooldownRemaining;
        return character;
    }

    [Fact]
    public void CheckAndTrigger_ReturnsFalse_WhenNoCatalogServiceProvided()
    {
        var svc = new ReactiveAbilityService(NullLogger<ReactiveAbilityService>.Instance);
        var character = new Character { Name = "Hero" };

        svc.CheckAndTriggerReactiveAbilities(character, "onDamageTaken").Should().BeFalse();
    }

    [Fact]
    public async Task CheckAndTrigger_ReturnsFalse_WhenCharacterHasNoLearnedAbilities()
    {
        var abilitySvc = await BuildAbilityDataService([]);
        var svc = new ReactiveAbilityService(NullLogger<ReactiveAbilityService>.Instance, abilitySvc);
        var character = new Character { Name = "Hero" };

        svc.CheckAndTriggerReactiveAbilities(character, "onDamageTaken").Should().BeFalse();
    }

    [Fact]
    public async Task CheckAndTrigger_ReturnsFalse_WhenAbilityIsNotReactive()
    {
        var regularAbility = new Ability
        {
            Id = "slash",
            DisplayName = "Slash",
            Traits = new Dictionary<string, object>
            {
                ["abilityClass"] = "active"
            }
        };
        var abilitySvc = await BuildAbilityDataService([regularAbility]);
        var svc = new ReactiveAbilityService(NullLogger<ReactiveAbilityService>.Instance, abilitySvc);

        svc.CheckAndTriggerReactiveAbilities(CharacterWithAbility("slash"), "onDamageTaken")
           .Should().BeFalse();
    }

    [Fact]
    public async Task CheckAndTrigger_ReturnsFalse_WhenTriggerConditionDoesNotMatch()
    {
        var ability = MakeReactiveAbility("counter-attack", "onDodge");
        var abilitySvc = await BuildAbilityDataService([ability]);
        var svc = new ReactiveAbilityService(NullLogger<ReactiveAbilityService>.Instance, abilitySvc);

        svc.CheckAndTriggerReactiveAbilities(CharacterWithAbility("counter-attack"), "onDamageTaken")
           .Should().BeFalse();
    }

    [Fact]
    public async Task CheckAndTrigger_ReturnsTrue_WhenTriggerConditionMatches()
    {
        var ability = MakeReactiveAbility("pain-response", "onDamageTaken");
        var abilitySvc = await BuildAbilityDataService([ability]);
        var svc = new ReactiveAbilityService(NullLogger<ReactiveAbilityService>.Instance, abilitySvc);

        svc.CheckAndTriggerReactiveAbilities(CharacterWithAbility("pain-response"), "onDamageTaken")
           .Should().BeTrue();
    }

    [Fact]
    public async Task CheckAndTrigger_ReturnsFalse_WhenAbilityIsOnCooldown()
    {
        var ability = MakeReactiveAbility("pain-response", "onDamageTaken", cooldown: 3);
        var abilitySvc = await BuildAbilityDataService([ability]);
        var svc = new ReactiveAbilityService(NullLogger<ReactiveAbilityService>.Instance, abilitySvc);

        // Set cooldown to 2 turns remaining
        var character = CharacterWithAbility("pain-response", cooldownRemaining: 2);

        svc.CheckAndTriggerReactiveAbilities(character, "onDamageTaken").Should().BeFalse();
    }

    [Fact]
    public async Task CheckAndTrigger_SetsCooldown_AfterTriggeringAbilityWithNonZeroCooldown()
    {
        var ability = MakeReactiveAbility("pain-response", "onDamageTaken", cooldown: 3);
        var abilitySvc = await BuildAbilityDataService([ability]);
        var svc = new ReactiveAbilityService(NullLogger<ReactiveAbilityService>.Instance, abilitySvc);
        var character = CharacterWithAbility("pain-response");

        svc.CheckAndTriggerReactiveAbilities(character, "onDamageTaken");

        character.AbilityCooldowns["pain-response"].Should().Be(3);
    }

    [Fact]
    public async Task CheckAndTrigger_HandlesDictionaryFormatTraits()
    {
        // Test the dictionary-format trait path (the alternative branch in IsReactiveAbility)
        var ability = new Ability
        {
            Id = "riposte",
            DisplayName = "Riposte",
            Traits = new Dictionary<string, object>
            {
                ["abilityClass"] = new Dictionary<string, object> { ["value"] = "reactive" },
                ["triggerCondition"] = new Dictionary<string, object> { ["value"] = "onBlock" }
            }
        };
        var abilitySvc = await BuildAbilityDataService([ability]);
        var svc = new ReactiveAbilityService(NullLogger<ReactiveAbilityService>.Instance, abilitySvc);

        svc.CheckAndTriggerReactiveAbilities(CharacterWithAbility("riposte"), "onBlock")
           .Should().BeTrue();
    }
}
