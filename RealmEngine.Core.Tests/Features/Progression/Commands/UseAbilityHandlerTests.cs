using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Progression.Commands;
using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Progression.Commands;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for UseAbilityHandler.
/// </summary>
public class UseAbilityHandlerTests
{
    private static async Task<(UseAbilityHandler handler, AbilityDataService service)> CreateHandlerAsync(IEnumerable<Ability>? abilities = null)
    {
        var mockRepo = new Mock<IAbilityRepository>();
        mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync((abilities ?? []).ToList());
        var abilitySvc = new AbilityDataService(mockRepo.Object);
        await abilitySvc.InitializeAsync();
        return (new UseAbilityHandler(abilitySvc, NullLogger<UseAbilityHandler>.Instance), abilitySvc);
    }

    private static Ability MakeAbility(string id, int manaCost = 10, int cooldown = 0, string? baseDamage = null) =>
        new()
        {
            Id = id,
            Slug = id,
            DisplayName = id,
            ManaCost = manaCost,
            Cooldown = cooldown,
            BaseDamage = baseDamage
        };

    private static Character CreateCharacterWithAbility(string abilityId, int mana = 100, int maxMana = 100) =>
        new Character { Name = "Hero", Mana = mana, MaxMana = maxMana }
            .TapLearnAbility(abilityId);

    [Fact]
    public async Task Returns_Failure_WhenAbilityNotInLearnedAbilities()
    {
        // Arrange
        var (handler, _) = await CreateHandlerAsync([MakeAbility("fireball")]);
        var character = new Character { Name = "Hero", Mana = 100 }; // NOT learned "fireball"
        var command = new UseAbilityCommand { User = character, AbilityId = "fireball" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("don't know");
    }

    [Fact]
    public async Task Returns_Failure_WhenAbilityNotInCatalog()
    {
        // Arrange - character has learned ability, but it's not in the data service
        var (handler, _) = await CreateHandlerAsync([]); // empty catalog
        var character = new Character { Name = "Hero", Mana = 100 };
        character.LearnedAbilities["ghost-ability"] = new CharacterAbility { AbilityId = "ghost-ability" };
        var command = new UseAbilityCommand { User = character, AbilityId = "ghost-ability" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Returns_Failure_WhenAbilityOnCooldown()
    {
        // Arrange
        var (handler, _) = await CreateHandlerAsync([MakeAbility("shield-bash", cooldown: 3)]);
        var character = CreateCharacterWithAbility("shield-bash");
        character.AbilityCooldowns["shield-bash"] = 2; // still 2 turns remaining

        var command = new UseAbilityCommand { User = character, AbilityId = "shield-bash" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("cooling down");
        result.Message.Should().Contain("2");
    }

    [Fact]
    public async Task Returns_Failure_WhenNotEnoughMana()
    {
        // Arrange
        var (handler, _) = await CreateHandlerAsync([MakeAbility("meteor", manaCost: 50)]);
        var character = CreateCharacterWithAbility("meteor", mana: 20); // only 20 mana, needs 50

        var command = new UseAbilityCommand { User = character, AbilityId = "meteor" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("mana");
        result.Message.Should().Contain("50");
    }

    [Fact]
    public async Task Succeeds_AndDeductsMana_WhenAllConditionsMet()
    {
        // Arrange
        var (handler, _) = await CreateHandlerAsync([MakeAbility("slash", manaCost: 15)]);
        var character = CreateCharacterWithAbility("slash", mana: 100);
        var command = new UseAbilityCommand { User = character, AbilityId = "slash" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.ManaCost.Should().Be(15);
        character.Mana.Should().Be(85, "mana cost should be deducted");
    }

    [Fact]
    public async Task Sets_Cooldown_AfterUse_WhenAbilityHasCooldown()
    {
        // Arrange
        var (handler, _) = await CreateHandlerAsync([MakeAbility("power-strike", manaCost: 10, cooldown: 3)]);
        var character = CreateCharacterWithAbility("power-strike");
        var command = new UseAbilityCommand { User = character, AbilityId = "power-strike" };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        character.AbilityCooldowns.Should().ContainKey("power-strike");
        character.AbilityCooldowns["power-strike"].Should().Be(3);
    }

    [Fact]
    public async Task DoesNotSet_Cooldown_WhenAbilityHasNoCooldown()
    {
        // Arrange
        var (handler, _) = await CreateHandlerAsync([MakeAbility("quick-slash", manaCost: 5, cooldown: 0)]);
        var character = CreateCharacterWithAbility("quick-slash");
        var command = new UseAbilityCommand { User = character, AbilityId = "quick-slash" };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        character.AbilityCooldowns.Should().NotContainKey("quick-slash");
    }

    [Fact]
    public async Task Increments_TimesUsed_OnSuccess()
    {
        // Arrange
        var (handler, _) = await CreateHandlerAsync([MakeAbility("cleave", manaCost: 8)]);
        var character = CreateCharacterWithAbility("cleave");
        character.LearnedAbilities["cleave"].TimesUsed = 4;
        var command = new UseAbilityCommand { User = character, AbilityId = "cleave" };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        character.LearnedAbilities["cleave"].TimesUsed.Should().Be(5);
    }

    [Fact]
    public async Task Returns_ZeroDamage_WhenAbilityHasNoBaseDamage()
    {
        // Arrange
        var (handler, _) = await CreateHandlerAsync([MakeAbility("taunt", manaCost: 5, baseDamage: null)]);
        var character = CreateCharacterWithAbility("taunt");
        var command = new UseAbilityCommand { User = character, AbilityId = "taunt" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.DamageDealt.Should().Be(0);
    }

    [Fact]
    public async Task Applies_Damage_ToTargetEnemy_WhenAbilityHasBaseDamage()
    {
        // Arrange - use a fixed-value damage (not dice) to keep result deterministic
        var ability = MakeAbility("force-strike", manaCost: 10, baseDamage: "5");
        var (handler, _) = await CreateHandlerAsync([ability]);
        var character = CreateCharacterWithAbility("force-strike");
        var enemy = new Enemy { Name = "Goblin", Health = 50, MaxHealth = 50 };
        var command = new UseAbilityCommand { User = character, AbilityId = "force-strike", TargetEnemy = enemy };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.DamageDealt.Should().BeGreaterThan(0);
        enemy.Health.Should().BeLessThan(50, "enemy should have taken damage");
    }
}

/// <summary>Extension to make test setup fluent.</summary>
internal static class CharacterTestExtensions
{
    /// <summary>Adds a learned ability to the character and returns it.</summary>
    public static Character TapLearnAbility(this Character character, string abilityId)
    {
        character.LearnedAbilities[abilityId] = new CharacterAbility { AbilityId = abilityId };
        return character;
    }
}
