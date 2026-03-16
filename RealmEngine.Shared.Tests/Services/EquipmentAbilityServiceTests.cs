using FluentAssertions;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Services;
using Xunit;

namespace RealmEngine.Shared.Tests.Services;

public class EquipmentAbilityServiceTests
{
    [Fact]
    public void GrantAbilitiesFromItem_Should_Add_Abilities_To_Character()
    {
        // Arrange
        var character = new Character
        {
            Name = "Test Hero",
            Level = 5
        };

        var item = new Item
        {
            Name = "Sword of Power",
            Traits = new Dictionary<string, TraitValue>
            {
                ["grantedAbilities"] = new TraitValue
                {
                    Type = TraitType.StringArray,
                    Value = new[] { "@abilities/active/offensive:power-attack", "@abilities/passive:weapon-mastery" }
                }
            }
        };

        // Act
        EquipmentAbilityService.GrantAbilitiesFromItem(character, item);

        // Assert
        character.EquipmentGrantedAbilities.Should().HaveCount(2);
        character.EquipmentGrantedAbilities.Should().ContainKey("active/offensive:power-attack");
        character.EquipmentGrantedAbilities.Should().ContainKey("passive:weapon-mastery");
        character.EquipmentGrantedAbilities["active/offensive:power-attack"].Should().Be(item.Id);
    }

    [Fact]
    public void GrantAbilitiesFromItem_Should_Not_Override_Learned_Abilities()
    {
        // Arrange
        var character = new Character
        {
            Name = "Test Hero",
            LearnedAbilities = new Dictionary<string, CharacterAbility>
            {
                ["active/offensive:power-attack"] = new CharacterAbility
                {
                    AbilityId = "active/offensive:power-attack",
                    TimesUsed = 50
                }
            }
        };

        var item = new Item
        {
            Name = "Sword of Power",
            Traits = new Dictionary<string, TraitValue>
            {
                ["grantedAbilities"] = new TraitValue
                {
                    Type = TraitType.StringArray,
                    Value = new[] { "@abilities/active/offensive:power-attack" }
                }
            }
        };

        // Act
        EquipmentAbilityService.GrantAbilitiesFromItem(character, item);

        // Assert
        character.EquipmentGrantedAbilities.Should().BeEmpty();
        character.LearnedAbilities.Should().ContainKey("active/offensive:power-attack");
    }

    [Fact]
    public void RevokeAbilitiesFromItem_Should_Remove_All_Abilities_From_Item()
    {
        // Arrange
        var item = new Item
        {
            Id = "item-123",
            Name = "Magic Ring"
        };

        var character = new Character
        {
            Name = "Test Hero",
            EquipmentGrantedAbilities = new Dictionary<string, string>
            {
                ["passive:fire-resistance"] = item.Id,
                ["passive:strength-boost"] = item.Id,
                ["active/defensive:shield"] = "other-item-id"
            }
        };

        // Act
        EquipmentAbilityService.RevokeAbilitiesFromItem(character, item);

        // Assert
        character.EquipmentGrantedAbilities.Should().HaveCount(1);
        character.EquipmentGrantedAbilities.Should().ContainKey("active/defensive:shield");
        character.EquipmentGrantedAbilities.Should().NotContainKey("passive:fire-resistance");
        character.EquipmentGrantedAbilities.Should().NotContainKey("passive:strength-boost");
    }

    [Fact]
    public void RecalculateEquipmentAbilities_Should_Scan_All_Equipped_Items()
    {
        // Arrange
        var character = new Character
        {
            Name = "Test Hero",
            EquippedMainHand = new Item
            {
                Name = "Fire Sword",
                Traits = new Dictionary<string, TraitValue>
                {
                    ["grantedAbilities"] = new TraitValue
                    {
                        Type = TraitType.StringArray,
                        Value = new[] { "@abilities/active/offensive:flame-strike" }
                    }
                }
            },
            EquippedHelmet = new Item
            {
                Name = "Helm of Wisdom",
                Traits = new Dictionary<string, TraitValue>
                {
                    ["grantedAbilities"] = new TraitValue
                    {
                        Type = TraitType.StringArray,
                        Value = new[] { "@abilities/passive:increased-mana" }
                    }
                }
            }
        };

        // Act
        EquipmentAbilityService.RecalculateEquipmentAbilities(character);

        // Assert
        character.EquipmentGrantedAbilities.Should().HaveCount(2);
        character.EquipmentGrantedAbilities.Should().ContainKey("active/offensive:flame-strike");
        character.EquipmentGrantedAbilities.Should().ContainKey("passive:increased-mana");
    }

    [Fact]
    public void HasAbility_Should_Return_True_For_Equipment_Granted_Abilities()
    {
        // Arrange
        var character = new Character
        {
            Name = "Test Hero",
            EquipmentGrantedAbilities = new Dictionary<string, string>
            {
                ["active/offensive:charge"] = "weapon-id"
            }
        };

        // Act & Assert
        character.HasAbility("active/offensive:charge").Should().BeTrue();
        character.HasAbility("unknown-ability").Should().BeFalse();
    }

    [Fact]
    public void GetAvailableAbilityIds_Should_Include_Both_Learned_And_Equipment_Granted()
    {
        // Arrange
        var character = new Character
        {
            Name = "Test Hero",
            LearnedAbilities = new Dictionary<string, CharacterAbility>
            {
                ["active/offensive:slash"] = new CharacterAbility { AbilityId = "active/offensive:slash" }
            },
            EquipmentGrantedAbilities = new Dictionary<string, string>
            {
                ["passive:defense-boost"] = "armor-id"
            }
        };

        // Act
        var abilities = character.GetAvailableAbilityIds().ToList();

        // Assert
        abilities.Should().HaveCount(2);
        abilities.Should().Contain("active/offensive:slash");
        abilities.Should().Contain("passive:defense-boost");
    }

    [Fact]
    public void GetAbilitySource_Should_Return_Learned_For_Learned_Abilities()
    {
        // Arrange
        var character = new Character
        {
            Name = "Test Hero",
            LearnedAbilities = new Dictionary<string, CharacterAbility>
            {
                ["active/offensive:fireball"] = new CharacterAbility { AbilityId = "active/offensive:fireball" }
            }
        };

        // Act
        var source = EquipmentAbilityService.GetAbilitySource(character, "active/offensive:fireball");

        // Assert
        source.Should().Be("Learned");
    }

    [Fact]
    public void GetAbilitySource_Should_Return_Item_Name_For_Equipment_Granted_Abilities()
    {
        // Arrange
        var item = new Item
        {
            Id = "sword-123",
            Name = "Legendary Sword"
        };

        var character = new Character
        {
            Name = "Test Hero",
            EquippedMainHand = item,
            EquipmentGrantedAbilities = new Dictionary<string, string>
            {
                ["active/offensive:divine-strike"] = item.Id
            }
        };

        // Act
        var source = EquipmentAbilityService.GetAbilitySource(character, "active/offensive:divine-strike");

        // Assert
        source.Should().Be("Legendary Sword");
    }

    [Fact]
    public void GrantAbilitiesFromItem_Should_Handle_Item_Without_Granted_Abilities()
    {
        // Arrange
        var character = new Character { Name = "Test Hero" };
        var item = new Item { Name = "Plain Sword" };

        // Act
        EquipmentAbilityService.GrantAbilitiesFromItem(character, item);

        // Assert
        character.EquipmentGrantedAbilities.Should().BeEmpty();
    }

    [Fact]
    public void GrantAbilitiesFromItem_Should_Handle_Null_Item()
    {
        // Arrange
        var character = new Character { Name = "Test Hero" };

        // Act
        EquipmentAbilityService.GrantAbilitiesFromItem(character, null!);

        // Assert
        character.EquipmentGrantedAbilities.Should().BeEmpty();
    }
}
