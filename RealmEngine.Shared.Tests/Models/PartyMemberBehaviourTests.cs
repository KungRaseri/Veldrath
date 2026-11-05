using FluentAssertions;
using RealmEngine.Shared.Models;
using Xunit;

namespace RealmEngine.Shared.Tests.Models;

/// <summary>
/// Behaviour tests for the derived combat statistics, resource clamping, experience and
/// role-driven level-up of <see cref="PartyMember"/>.
/// </summary>
[Trait("Category", "Unit")]
public class PartyMemberBehaviourTests
{
    // ── Defaults and liveness ────────────────────────────────────────────────

    [Fact]
    public void PartyMember_DefaultsToALevelOneDpsWithFullResources()
    {
        // Act
        var member = new PartyMember();

        // Assert
        member.Id.Should().NotBeNullOrEmpty();
        member.Level.Should().Be(1);
        member.Health.Should().Be(100);
        member.MaxHealth.Should().Be(100);
        member.Mana.Should().Be(50);
        member.MaxMana.Should().Be(50);
        member.Role.Should().Be(PartyRole.DPS);
        member.Behavior.Should().Be(AIBehavior.Balanced);
        member.Strength.Should().Be(10);
        member.Dexterity.Should().Be(10);
        member.Constitution.Should().Be(10);
        member.Inventory.Should().BeEmpty();
        member.ActiveStatusEffects.Should().BeEmpty();
    }

    [Fact]
    public void IsAlive_IsTrueUntilHealthReachesZero()
    {
        // Arrange
        var member = new PartyMember();

        // Act & Assert
        member.IsAlive.Should().BeTrue();
        member.Health = 0;
        member.IsAlive.Should().BeFalse();
    }

    // ── Derived stats ────────────────────────────────────────────────────────

    [Fact]
    public void GetAttack_IsStrengthPlusTwoPerLevel()
    {
        // Arrange
        var member = new PartyMember { Strength = 12, Level = 3 };

        // Act
        var attack = member.GetAttack();

        // Assert
        attack.Should().Be(18); // 12 + 3*2
    }

    [Fact]
    public void GetAttack_AddsTheEquippedWeaponAttackTrait()
    {
        // Arrange
        var weapon = new Item { Name = "Iron Sword" };
        weapon.Traits["attack"] = new TraitValue(7, TraitType.Number);
        var member = new PartyMember { Strength = 10, Level = 1, EquippedWeapon = weapon };

        // Act
        var attack = member.GetAttack();

        // Assert
        attack.Should().Be(19); // 10 + 2 + 7
    }

    [Fact]
    public void GetAttack_IgnoresAWeaponWithoutAnAttackTrait()
    {
        // Arrange
        var member = new PartyMember { Strength = 10, Level = 1, EquippedWeapon = new Item { Name = "Stick" } };

        // Act
        var attack = member.GetAttack();

        // Assert
        attack.Should().Be(12);
    }

    [Fact]
    public void GetDefense_IsConstitutionPlusTwoPerLevel()
    {
        // Arrange
        var member = new PartyMember { Constitution = 14, Level = 2 };

        // Act
        var defense = member.GetDefense();

        // Assert
        defense.Should().Be(18); // 14 + 4
    }

    [Fact]
    public void GetDefense_AddsTheEquippedArmorDefenseTrait()
    {
        // Arrange
        var armor = new Item { Name = "Chainmail" };
        armor.Traits["defense"] = new TraitValue(6, TraitType.Number);
        var member = new PartyMember { Constitution = 10, Level = 1, EquippedArmor = armor };

        // Act
        var defense = member.GetDefense();

        // Assert
        defense.Should().Be(18); // 10 + 2 + 6
    }

    [Fact]
    public void GetDodgeChance_IsOneOverTwoHundredDexterityPlusFivePercent()
    {
        // Arrange
        var member = new PartyMember { Dexterity = 40 };

        // Act
        var dodge = member.GetDodgeChance();

        // Assert
        dodge.Should().BeApproximately(0.25, 1e-9); // 40/200 + 0.05
    }

    [Fact]
    public void GetCriticalChance_IsTenPercentOfOneHundredthDexterity()
    {
        // Arrange
        var member = new PartyMember { Dexterity = 50 };

        // Act
        var crit = member.GetCriticalChance();

        // Assert
        crit.Should().BeApproximately(0.05, 1e-9); // 50/100 * 0.10
    }

    // ── Resource clamping ────────────────────────────────────────────────────

    [Fact]
    public void Heal_ClampsAtMaxHealth()
    {
        // Arrange
        var member = new PartyMember { Health = 90, MaxHealth = 100 };

        // Act
        member.Heal(25);

        // Assert
        member.Health.Should().Be(100);
    }

    [Fact]
    public void Heal_BelowTheCap_AddsTheFullAmount()
    {
        // Arrange
        var member = new PartyMember { Health = 40, MaxHealth = 100 };

        // Act
        member.Heal(25);

        // Assert
        member.Health.Should().Be(65);
    }

    [Fact]
    public void RestoreMana_ClampsAtMaxMana()
    {
        // Arrange
        var member = new PartyMember { Mana = 40, MaxMana = 50 };

        // Act
        member.RestoreMana(30);

        // Assert
        member.Mana.Should().Be(50);
    }

    [Fact]
    public void TakeDamage_FloorsAtZero()
    {
        // Arrange
        var member = new PartyMember { Health = 10 };

        // Act
        member.TakeDamage(50);

        // Assert
        member.Health.Should().Be(0);
        member.IsAlive.Should().BeFalse();
    }

    [Fact]
    public void TakeDamage_ReducesHealthWhenTheHitIsSurvivable()
    {
        // Arrange
        var member = new PartyMember { Health = 80 };

        // Act
        member.TakeDamage(30);

        // Assert
        member.Health.Should().Be(50);
    }

    // ── Experience and levelling ────────────────────────────────────────────

    [Fact]
    public void GainExperience_BelowTheThreshold_AccumulatesWithoutLevelling()
    {
        // Arrange
        var member = new PartyMember { Level = 1 }; // needs 100 XP

        // Act
        var levelled = member.GainExperience(99);

        // Assert
        levelled.Should().BeFalse();
        member.Level.Should().Be(1);
        member.Experience.Should().Be(99);
    }

    [Fact]
    public void GainExperience_AtTheThreshold_LevelsUpAndCarriesTheRemainder()
    {
        // Arrange
        var member = new PartyMember { Level = 1 }; // needs 100 XP

        // Act
        var levelled = member.GainExperience(130);

        // Assert
        levelled.Should().BeTrue();
        member.Level.Should().Be(2);
        member.Experience.Should().Be(30);
    }

    [Fact]
    public void GainExperience_LevelUp_RaisesMaxHealthAndManaAndRefillsBoth()
    {
        // Arrange
        var member = new PartyMember { Level = 1, Health = 10, Mana = 10 };

        // Act
        member.GainExperience(100);

        // Assert
        member.MaxHealth.Should().Be(120); // 100 + 10 + Constitution(10)
        member.MaxMana.Should().Be(65); // 50 + 5 + Wisdom(10)
        member.Health.Should().Be(120);
        member.Mana.Should().Be(65);
    }

    [Fact]
    public void GainExperience_LevelUp_TankGrowsConstitutionAndStrength()
    {
        // Arrange
        var member = new PartyMember { Level = 1, Role = PartyRole.Tank };

        // Act
        member.GainExperience(100);

        // Assert
        member.Strength.Should().Be(11);
        member.Constitution.Should().Be(12);
        member.Dexterity.Should().Be(10);
    }

    [Fact]
    public void GainExperience_LevelUp_DpsGrowsStrengthAndDexterity()
    {
        // Arrange
        var member = new PartyMember { Level = 1, Role = PartyRole.DPS };

        // Act
        member.GainExperience(100);

        // Assert
        member.Strength.Should().Be(12);
        member.Dexterity.Should().Be(11);
        member.Constitution.Should().Be(10);
    }

    [Fact]
    public void GainExperience_LevelUp_HealerGrowsWisdomAndIntelligence()
    {
        // Arrange
        var member = new PartyMember { Level = 1, Role = PartyRole.Healer };

        // Act
        member.GainExperience(100);

        // Assert
        member.Wisdom.Should().Be(12);
        member.Intelligence.Should().Be(11);
        member.Strength.Should().Be(10);
        member.Charisma.Should().Be(10);
    }

    [Fact]
    public void GainExperience_LevelUp_SupportGrowsIntelligenceAndCharisma()
    {
        // Arrange
        var member = new PartyMember { Level = 1, Role = PartyRole.Support };

        // Act
        member.GainExperience(100);

        // Assert
        member.Intelligence.Should().Be(12);
        member.Charisma.Should().Be(11);
        member.Strength.Should().Be(10);
        member.Dexterity.Should().Be(10);
    }

    [Fact]
    public void GainExperience_MultipleLevelsAreAppliedOneAtATime()
    {
        // Arrange
        var member = new PartyMember { Level = 1 };

        // Act — 100 XP levels to 2 (leaving 0), then another 200 levels to 3
        member.GainExperience(100);
        var levelled = member.GainExperience(200);

        // Assert
        levelled.Should().BeTrue();
        member.Level.Should().Be(3);
        member.Experience.Should().Be(0); // 200 - 200 (level 2 needs 200)
    }
}
