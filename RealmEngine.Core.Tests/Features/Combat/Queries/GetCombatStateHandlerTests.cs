using FluentAssertions;
using RealmEngine.Core.Features.Combat.Queries.GetCombatState;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Combat.Queries;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for GetCombatStateHandler.
/// </summary>
public class GetCombatStateHandlerTests
{
    [Fact]
    public async Task Handle_Should_Return_Combat_State()
    {
        // Arrange
        var handler = new GetCombatStateHandler();
        var player = new Character { Name = "Hero", Health = 80, MaxHealth = 100 };
        var enemy = new Enemy { Name = "Goblin", Health = 30, MaxHealth = 50 };
        var query = new GetCombatStateQuery { Player = player, Enemy = enemy };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.PlayerHealthPercentage.Should().Be(80);
        result.EnemyHealthPercentage.Should().Be(60);
    }

    [Fact]
    public async Task Handle_Should_Calculate_Player_Health_Percentage()
    {
        // Arrange
        var handler = new GetCombatStateHandler();
        var player = new Character { Health = 25, MaxHealth = 100 };
        var enemy = new Enemy { Health = 50, MaxHealth = 50 };
        var query = new GetCombatStateQuery { Player = player, Enemy = enemy };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.PlayerHealthPercentage.Should().Be(25);
    }

    [Fact]
    public async Task Handle_Should_Calculate_Enemy_Health_Percentage()
    {
        // Arrange
        var handler = new GetCombatStateHandler();
        var player = new Character { Health = 100, MaxHealth = 100 };
        var enemy = new Enemy { Health = 10, MaxHealth = 100 };
        var query = new GetCombatStateQuery { Player = player, Enemy = enemy };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.EnemyHealthPercentage.Should().Be(10);
    }

    [Fact]
    public async Task Handle_Should_Set_PlayerCanFlee_To_True()
    {
        // Arrange
        var handler = new GetCombatStateHandler();
        var player = new Character { Health = 50, MaxHealth = 100 };
        var enemy = new Enemy { Health = 50, MaxHealth = 100 };
        var query = new GetCombatStateQuery { Player = player, Enemy = enemy };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.PlayerCanFlee.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_Detect_When_Player_Has_Consumable_Items()
    {
        // Arrange
        var handler = new GetCombatStateHandler();
        var player = new Character { Health = 50, MaxHealth = 100 };
        player.Inventory.Add(new Item { Name = "Health Potion", Type = ItemType.Consumable });
        var enemy = new Enemy { Health = 50, MaxHealth = 100 };
        var query = new GetCombatStateQuery { Player = player, Enemy = enemy };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.PlayerHasItems.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_Return_False_When_Player_Has_No_Consumables()
    {
        // Arrange
        var handler = new GetCombatStateHandler();
        var player = new Character { Health = 50, MaxHealth = 100 };
        player.Inventory.Add(new Item { Name = "Sword", Type = ItemType.Weapon });
        var enemy = new Enemy { Health = 50, MaxHealth = 100 };
        var query = new GetCombatStateQuery { Player = player, Enemy = enemy };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.PlayerHasItems.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_Return_Available_Actions_Without_Abilities_Or_Spells()
    {
        // Arrange
        var handler = new GetCombatStateHandler();
        var player = new Character { Health = 50, MaxHealth = 100 };
        var enemy = new Enemy { Health = 50, MaxHealth = 100 };
        var query = new GetCombatStateQuery { Player = player, Enemy = enemy };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.AvailableActions.Should().Contain("Attack");
        result.AvailableActions.Should().Contain("Defend");
        result.AvailableActions.Should().Contain("Use Item");
        result.AvailableActions.Should().Contain("Flee");
        result.AvailableActions.Should().HaveCount(4);
    }

    [Fact]
    public async Task Handle_Should_Include_Use_Ability_When_Player_Has_Available_Abilities()
    {
        // Arrange
        var handler = new GetCombatStateHandler();
        var player = new Character { Health = 50, MaxHealth = 100, Mana = 50, MaxMana = 100 };
        var ability = new CharacterAbility { AbilityId = "test-ability" };
        player.LearnedAbilities.Add("test-ability", ability);
        player.AbilityCooldowns["test-ability"] = 0; // Not on cooldown

        var enemy = new Enemy { Health = 50, MaxHealth = 100 };
        var query = new GetCombatStateQuery { Player = player, Enemy = enemy };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.AvailableActions.Should().Contain("Use Ability");
        result.AvailableAbilities.Should().Contain("test-ability");
    }

    [Fact]
    public async Task Handle_Should_Not_Include_Ability_On_Cooldown()
    {
        // Arrange
        var handler = new GetCombatStateHandler();
        var player = new Character { Health = 50, MaxHealth = 100, Mana = 50, MaxMana = 100 };
        var ability = new CharacterAbility { AbilityId = "test-ability" };
        player.LearnedAbilities.Add("test-ability", ability);
        player.AbilityCooldowns["test-ability"] = 2; // On cooldown

        var enemy = new Enemy { Health = 50, MaxHealth = 100 };
        var query = new GetCombatStateQuery { Player = player, Enemy = enemy };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.AvailableActions.Should().NotContain("Use Ability");
        result.AvailableAbilities.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Include_Cast_Spell_When_Player_Has_Available_Spells()
    {
        // Arrange
        var handler = new GetCombatStateHandler();
        var player = new Character { Health = 50, MaxHealth = 100, Mana = 50, MaxMana = 100 };
        var spell = new CharacterSpell { SpellId = "test-spell" };
        player.LearnedSpells.Add("test-spell", spell);
        player.SpellCooldowns["test-spell"] = 0; // Not on cooldown

        var enemy = new Enemy { Health = 50, MaxHealth = 100 };
        var query = new GetCombatStateQuery { Player = player, Enemy = enemy };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.AvailableActions.Should().Contain("Cast Spell");
        result.AvailableSpells.Should().Contain("test-spell");
    }

    [Fact]
    public async Task Handle_Should_Not_Include_Spell_On_Cooldown()
    {
        // Arrange
        var handler = new GetCombatStateHandler();
        var player = new Character { Health = 50, MaxHealth = 100, Mana = 50, MaxMana = 100 };
        var spell = new CharacterSpell { SpellId = "test-spell" };
        player.LearnedSpells.Add("test-spell", spell);
        player.SpellCooldowns["test-spell"] = 3; // On cooldown

        var enemy = new Enemy { Health = 50, MaxHealth = 100 };
        var query = new GetCombatStateQuery { Player = player, Enemy = enemy };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.AvailableActions.Should().NotContain("Cast Spell");
        result.AvailableSpells.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Include_Both_Abilities_And_Spells_When_Available()
    {
        // Arrange
        var handler = new GetCombatStateHandler();
        var player = new Character { Health = 50, MaxHealth = 100, Mana = 50, MaxMana = 100 };

        var ability = new CharacterAbility { AbilityId = "test-ability" };
        player.LearnedAbilities.Add("test-ability", ability);
        player.AbilityCooldowns["test-ability"] = 0;

        var spell = new CharacterSpell { SpellId = "test-spell" };
        player.LearnedSpells.Add("test-spell", spell);
        player.SpellCooldowns["test-spell"] = 0;

        var enemy = new Enemy { Health = 50, MaxHealth = 100 };
        var query = new GetCombatStateQuery { Player = player, Enemy = enemy };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.AvailableActions.Should().Contain("Use Ability");
        result.AvailableActions.Should().Contain("Cast Spell");
        result.AvailableAbilities.Should().Contain("test-ability");
        result.AvailableSpells.Should().Contain("test-spell");
        result.AvailableActions.Should().HaveCount(6); // Attack, Use Ability, Cast Spell, Defend, Use Item, Flee
    }

    [Fact]
    public async Task Handle_Should_Order_Actions_Correctly_With_Abilities_And_Spells()
    {
        // Arrange
        var handler = new GetCombatStateHandler();
        var player = new Character { Health = 50, MaxHealth = 100, Mana = 50, MaxMana = 100 };

        var ability = new CharacterAbility { AbilityId = "test-ability" };
        player.LearnedAbilities.Add("test-ability", ability);
        player.AbilityCooldowns["test-ability"] = 0;

        var spell = new CharacterSpell { SpellId = "test-spell" };
        player.LearnedSpells.Add("test-spell", spell);
        player.SpellCooldowns["test-spell"] = 0;

        var enemy = new Enemy { Health = 50, MaxHealth = 100 };
        var query = new GetCombatStateQuery { Player = player, Enemy = enemy };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.AvailableActions[0].Should().Be("Attack");
        result.AvailableActions[1].Should().Be("Use Ability");
        result.AvailableActions[2].Should().Be("Cast Spell");
        result.AvailableActions[3].Should().Be("Defend");
        result.AvailableActions[4].Should().Be("Use Item");
        result.AvailableActions[5].Should().Be("Flee");
    }

    [Theory]
    [InlineData(100, 100, 100)]
    [InlineData(50, 100, 50)]
    [InlineData(1, 100, 1)]
    [InlineData(0, 100, 0)]
    public async Task Handle_Should_Calculate_Correct_Health_Percentages(int health, int maxHealth, int expectedPercentage)
    {
        // Arrange
        var handler = new GetCombatStateHandler();
        var player = new Character { Health = health, MaxHealth = maxHealth };
        var enemy = new Enemy { Health = 50, MaxHealth = 50 };
        var query = new GetCombatStateQuery { Player = player, Enemy = enemy };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.PlayerHealthPercentage.Should().Be(expectedPercentage);
    }
}
