using FluentAssertions;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Xunit;

namespace RealmEngine.Shared.Tests.Models;

/// <summary>
/// Behaviour tests for the socket domain notifications and the lightweight
/// <see cref="ICombatSettings"/> presets.
/// </summary>
[Trait("Category", "Unit")]
public class SocketEventAndPresetTests
{
    // ── ItemSocketed ─────────────────────────────────────────────────────────

    [Fact]
    public void ItemSocketed_CarriesTheSocketContextAndAppliedTraits()
    {
        // Arrange
        var traits = new Dictionary<string, TraitValue> { ["strength"] = new(2, TraitType.Number) };

        // Act
        var notification = new ItemSocketed("item-1", "Ruby", SocketType.Gem, 0, traits);

        // Assert
        notification.ItemId.Should().Be("item-1");
        notification.SocketableItemName.Should().Be("Ruby");
        notification.SocketType.Should().Be(SocketType.Gem);
        notification.SocketIndex.Should().Be(0);
        notification.AppliedTraits.Should().BeSameAs(traits);
    }

    [Fact]
    public void ItemSocketed_IsAValueRecordWithStructuralEquality()
    {
        // Arrange
        var traits = new Dictionary<string, TraitValue> { ["strength"] = new(2, TraitType.Number) };
        var left = new ItemSocketed("item-1", "Ruby", SocketType.Gem, 0, traits);
        var right = new ItemSocketed("item-1", "Ruby", SocketType.Gem, 0, traits);

        // Act & Assert
        left.Should().Be(right);
        left.GetHashCode().Should().Be(right.GetHashCode());
        left.Should().BeAssignableTo<MediatR.INotification>();
    }

    [Fact]
    public void ItemSocketed_DiffersWhenTheSocketIndexDiffers()
    {
        // Arrange
        var traits = new Dictionary<string, TraitValue>();
        var left = new ItemSocketed("item-1", "Ruby", SocketType.Gem, 0, traits);
        var right = new ItemSocketed("item-1", "Ruby", SocketType.Gem, 1, traits);

        // Act & Assert
        left.Should().NotBe(right);
    }

    // ── ItemUnsocketed ───────────────────────────────────────────────────────

    [Fact]
    public void ItemUnsocketed_CarriesTheRemovalCost()
    {
        // Act
        var notification = new ItemUnsocketed("item-1", "Ruby", SocketType.Gem, 1, 250);

        // Assert
        notification.ItemId.Should().Be("item-1");
        notification.SocketableItemName.Should().Be("Ruby");
        notification.SocketType.Should().Be(SocketType.Gem);
        notification.SocketIndex.Should().Be(1);
        notification.GoldCost.Should().Be(250);
    }

    [Fact]
    public void ItemUnsocketed_IsAValueRecordWithStructuralEquality()
    {
        // Arrange
        var left = new ItemUnsocketed("item-1", "Ruby", SocketType.Gem, 1, 250);
        var right = new ItemUnsocketed("item-1", "Ruby", SocketType.Gem, 1, 250);

        // Act & Assert
        left.Should().Be(right);
        left.Should().BeAssignableTo<MediatR.INotification>();
    }

    // ── SocketLinkActivated ──────────────────────────────────────────────────

    [Fact]
    public void SocketLinkActivated_CarriesTheLinkBonus()
    {
        // Act
        var notification = new SocketLinkActivated("item-1", 2, 3, 1.25);

        // Assert
        notification.ItemId.Should().Be("item-1");
        notification.LinkGroupId.Should().Be(2);
        notification.LinkSize.Should().Be(3);
        notification.BonusMultiplier.Should().Be(1.25);
    }

    [Fact]
    public void SocketLinkActivated_ComparesByValue()
    {
        // Arrange
        var left = new SocketLinkActivated("item-1", 2, 3, 1.25);
        var right = new SocketLinkActivated("item-1", 2, 3, 1.25);
        var different = new SocketLinkActivated("item-1", 2, 3, 1.5);

        // Act & Assert
        left.Should().Be(right);
        left.Should().NotBe(different);
        left.Should().BeAssignableTo<MediatR.INotification>();
    }

    // ── NormalCombatSettings ─────────────────────────────────────────────────

    [Fact]
    public void NormalCombatSettings_UsesNeutralMultipliersAndNoPermadeath()
    {
        // Act
        var settings = new NormalCombatSettings();

        // Assert
        settings.PlayerDamageMultiplier.Should().Be(1.0);
        settings.EnemyDamageMultiplier.Should().Be(1.0);
        settings.EnemyHealthMultiplier.Should().Be(1.0);
        settings.GoldXPMultiplier.Should().Be(1.0);
        settings.IsPermadeath.Should().BeFalse();
        settings.Should().BeAssignableTo<ICombatSettings>();
    }

    // ── HardcoreCombatSettings ───────────────────────────────────────────────

    [Fact]
    public void HardcoreCombatSettings_UsesNeutralMultipliersButEnablesPermadeath()
    {
        // Act
        var settings = new HardcoreCombatSettings();

        // Assert
        settings.PlayerDamageMultiplier.Should().Be(1.0);
        settings.EnemyDamageMultiplier.Should().Be(1.0);
        settings.EnemyHealthMultiplier.Should().Be(1.0);
        settings.GoldXPMultiplier.Should().Be(1.0);
        settings.IsPermadeath.Should().BeTrue();
    }

    [Fact]
    public void TheTwoPresets_DifferOnlyInPermadeath()
    {
        // Arrange
        ICombatSettings normal = new NormalCombatSettings();
        ICombatSettings hardcore = new HardcoreCombatSettings();

        // Act & Assert
        hardcore.PlayerDamageMultiplier.Should().Be(normal.PlayerDamageMultiplier);
        hardcore.EnemyDamageMultiplier.Should().Be(normal.EnemyDamageMultiplier);
        hardcore.EnemyHealthMultiplier.Should().Be(normal.EnemyHealthMultiplier);
        hardcore.GoldXPMultiplier.Should().Be(normal.GoldXPMultiplier);
        hardcore.IsPermadeath.Should().NotBe(normal.IsPermadeath);
    }
}
