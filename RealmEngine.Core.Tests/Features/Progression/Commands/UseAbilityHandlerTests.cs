using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Combat.Commands;
using RealmEngine.Core.Features.Progression.Commands;
using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Core.Tests.Infrastructure;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Progression.Commands;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for UsePowerHandler.
/// </summary>
public class UsePowerHandlerTests
{
    private static async Task<(UsePowerHandler handler, PowerDataService service)> CreateHandlerAsync(
        IEnumerable<Power>? abilities = null,
        IRandomSource? randomSource = null,
        IMediator? mediator = null)
    {
        var mockRepo = new Mock<IPowerRepository>();
        mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync((abilities ?? []).ToList());
        var abilitySvc = new PowerDataService(mockRepo.Object);
        await abilitySvc.InitializeAsync();
        return (new UsePowerHandler(abilitySvc, NullLogger<UsePowerHandler>.Instance, mediator, randomSource), abilitySvc);
    }

    private static Power MakeAbility(string id, int manaCost = 10, int cooldown = 0, string? baseDamage = null) =>
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
        var command = new UsePowerCommand { User = character, PowerId = "fireball" };

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
        var command = new UsePowerCommand { User = character, PowerId = "ghost-ability" };

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

        var command = new UsePowerCommand { User = character, PowerId = "shield-bash" };

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

        var command = new UsePowerCommand { User = character, PowerId = "meteor" };

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
        var command = new UsePowerCommand { User = character, PowerId = "slash" };

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
        var command = new UsePowerCommand { User = character, PowerId = "power-strike" };

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
        var command = new UsePowerCommand { User = character, PowerId = "quick-slash" };

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
        var command = new UsePowerCommand { User = character, PowerId = "cleave" };

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
        var command = new UsePowerCommand { User = character, PowerId = "taunt" };

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
        var command = new UsePowerCommand { User = character, PowerId = "force-strike", TargetEnemy = enemy };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.DamageDealt.Should().BeGreaterThan(0);
        enemy.Health.Should().BeLessThan(50, "enemy should have taken damage");
    }

    [Fact]
    public async Task Rolls_Dice_Damage_From_Injected_Random_Source()
    {
        // Arrange - "2d6" with a scripted 3 + 4; the default character has Strength 0,
        // so the physical damage bonus adds nothing and the total is exactly the scripted sum.
        var random = FakeRandomSource.Returning(3, 4);
        var ability = MakeAbility("burning-strike", manaCost: 10, baseDamage: "2d6");
        var (handler, _) = await CreateHandlerAsync([ability], random);
        var character = CreateCharacterWithAbility("burning-strike");
        character.Strength = 0;      // no physical damage bonus on top of the dice
        character.Intelligence = 0;  // and no magic bonus either
        var enemy = new Enemy { Name = "Goblin", Health = 50, MaxHealth = 50 };
        var command = new UsePowerCommand { User = character, PowerId = "burning-strike", TargetEnemy = enemy };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        random.IntCallCount.Should().Be(2, "one draw per die in 2d6");
        random.RemainingInts.Should().Be(0, "the handler must consume the injected source, not a static one");
        result.DamageDealt.Should().Be(7);
        enemy.Health.Should().Be(43, "the scripted damage must reach the target");
    }

    [Fact]
    public async Task Rolls_Healing_From_Injected_Random_Source_And_Clamps_At_MaxHealth()
    {
        // Arrange - a heal power still rolls its damage dice first (no target, so nothing is hurt),
        // then rolls the healing dice. Script: 2 damage, 5 healing.
        var random = FakeRandomSource.Returning(2, 5);
        var ability = MakeAbility("mend", manaCost: 5, baseDamage: "1d6");
        ability.EffectType = PowerEffectType.Heal;
        var (handler, _) = await CreateHandlerAsync([ability], random);
        var character = CreateCharacterWithAbility("mend");
        character.Health = 95;
        character.MaxHealth = 100;
        var command = new UsePowerCommand { User = character, PowerId = "mend" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        random.IntCallCount.Should().Be(2);
        result.HealingDone.Should().Be(5);
        result.Message.Should().Contain("Healed 5 HP");
        character.Health.Should().Be(100, "healing must not exceed MaxHealth");
    }

    [Fact]
    public async Task Applies_Status_Effect_When_Injected_Roll_Is_Below_Chance()
    {
        // Arrange - 50% status chance, scripted roll 49, so the effect lands.
        var random = FakeRandomSource.Returning(49);
        var ability = MakeAbility("venom-bite", manaCost: 5);
        ability.Traits["statusEffect"] = "poisoned";
        ability.Traits["statusChance"] = 50;

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<ApplyStatusEffectCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApplyStatusEffectResult { Success = true, Message = "applied" });

        var (handler, _) = await CreateHandlerAsync([ability], random, mediator.Object);
        var character = CreateCharacterWithAbility("venom-bite");
        var enemy = new Enemy { Name = "Goblin", Health = 50, MaxHealth = 50 };

        // Act
        await handler.Handle(
            new UsePowerCommand { User = character, PowerId = "venom-bite", TargetEnemy = enemy },
            CancellationToken.None);

        // Assert
        random.IntCallCount.Should().Be(1, "the status chance roll must come from the injected source");
        mediator.Verify(
            m => m.Send(
                It.Is<ApplyStatusEffectCommand>(command =>
                    command.TargetEnemy == enemy && command.Effect.Type == StatusEffectType.Poisoned),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Skips_Status_Effect_When_Injected_Roll_Reaches_Chance()
    {
        // Arrange - 50% status chance, scripted roll 50, so the effect is not applied.
        var random = FakeRandomSource.Returning(50);
        var ability = MakeAbility("venom-bite", manaCost: 5);
        ability.Traits["statusEffect"] = "poisoned";
        ability.Traits["statusChance"] = 50;

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<ApplyStatusEffectCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApplyStatusEffectResult { Success = true, Message = "applied" });

        var (handler, _) = await CreateHandlerAsync([ability], random, mediator.Object);
        var character = CreateCharacterWithAbility("venom-bite");
        var enemy = new Enemy { Name = "Goblin", Health = 50, MaxHealth = 50 };

        // Act
        await handler.Handle(
            new UsePowerCommand { User = character, PowerId = "venom-bite", TargetEnemy = enemy },
            CancellationToken.None);

        // Assert
        random.IntCallCount.Should().Be(1);
        mediator.Verify(
            m => m.Send(It.IsAny<ApplyStatusEffectCommand>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "a roll at or above the chance must skip the effect");
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
