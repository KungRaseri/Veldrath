using FluentAssertions;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Utilities;
using Xunit;

namespace RealmEngine.Shared.Tests.Models;

/// <summary>
/// Closes the remaining non-numeric merge arms: text/boolean conflicts contributed by
/// player enchantments and socketed content, the unknown-rarity upgrade ceiling, and the
/// defensive switch arms that only an out-of-range <see cref="TraitType"/> can reach.
/// </summary>
[Trait("Category", "Unit")]
public class ItemResidualBranchTests
{
    private sealed class FakeTraitable : ITraitable
    {
        public Dictionary<string, TraitValue> Traits { get; } = new();
    }

    // ── Player-enchantment text merge arms ───────────────────────────────────

    [Fact]
    public void GetTotalTraits_PlayerEnchantmentTextTraitOverridesTheBaseTrait()
    {
        // Arrange
        var item = new Item { Name = "Longsword" };
        item.Traits["Element"] = new TraitValue("physical", TraitType.String);
        item.PlayerEnchantments.Add(new Enchantment
        {
            Name = "Flaming",
            Traits = { ["Element"] = new TraitValue("fire", TraitType.String) },
        });

        // Act
        var result = item.GetTotalTraits();

        // Assert
        result["Element"].AsString().Should().Be("fire");
    }

    [Fact]
    public void GetTotalTraits_PlayerEnchantmentAddsTraitsTheItemDidNotHave()
    {
        // Arrange
        var item = new Item { Name = "Longsword" };
        item.PlayerEnchantments.Add(new Enchantment
        {
            Name = "Blessed",
            Traits = { ["aura"] = new TraitValue("holy", TraitType.String) },
        });

        // Act
        var result = item.GetTotalTraits();

        // Assert
        result.Should().ContainKey("aura");
        result["aura"].AsString().Should().Be("holy");
    }

    // ── Socket-content merge arms ────────────────────────────────────────────

    [Fact]
    public void GetTotalTraits_SocketContentTextTraitOverridesTheBaseTrait()
    {
        // Arrange
        var item = new Item { Name = "Longsword" };
        item.Traits["Element"] = new TraitValue("physical", TraitType.String);

        var rune = new Rune { Name = "Frost Rune" };
        rune.Traits["Element"] = new TraitValue("ice", TraitType.String);
        item.Sockets[SocketType.Rune] = [new Socket { Type = SocketType.Rune, Content = rune }];

        // Act
        var result = item.GetTotalTraits();

        // Assert
        result["Element"].AsString().Should().Be("ice");
    }

    [Fact]
    public void GetTotalTraits_SocketContentAddsTraitsTheItemDidNotHave()
    {
        // Arrange
        var item = new Item { Name = "Longsword" };
        var orb = new Orb { Name = "Strike Orb" };
        orb.Traits["proc"] = new TraitValue("cleave", TraitType.String);
        item.Sockets[SocketType.Orb] = [new Socket { Type = SocketType.Orb, Content = orb }];

        // Act
        var result = item.GetTotalTraits();

        // Assert
        result["proc"].AsString().Should().Be("cleave");
    }

    // ── Unknown rarity ──────────────────────────────────────────────────────

    [Fact]
    public void GetMaxUpgradeLevel_ForAnUndefinedRarity_ReturnsZero()
    {
        // Arrange
        var item = new Item { Rarity = (ItemRarity)99, UpgradeLevel = 0 };

        // Act & Assert
        item.GetMaxUpgradeLevel().Should().Be(0);
        item.CanUpgrade().Should().BeFalse();
    }

    // ── Defensive switch arms ───────────────────────────────────────────────

    [Fact]
    public void DebugTraits_FallsBackToTheTraitValueToStringForAnUnknownTraitType()
    {
        // Arrange
        var entity = new FakeTraitable();
        entity.Traits["odd"] = new TraitValue("mystery", (TraitType)99);

        // Act
        var text = TraitApplicator.DebugTraits(entity);

        // Assert — the defensive arm renders the TraitValue itself
        text.Should().Be($"  odd: {typeof(TraitValue).FullName}");
    }

    [Fact]
    public void GetTraitsForUI_FallsBackToTheTraitValueToStringForAnUnknownTraitType()
    {
        // Arrange
        var entity = new FakeTraitable();
        entity.Traits["odd"] = new TraitValue(null, (TraitType)99);

        // Act
        var ui = TraitApplicator.GetTraitsForUI(entity);

        // Assert
        ui.Should().ContainKey("odd");
        ui["odd"].Should().Be(typeof(TraitValue).FullName);
    }

    // ── Reputation ladder: every rung has a successor except Exalted ─────────

    [Theory]
    [InlineData(-6000, ReputationLevel.Unfriendly)]
    [InlineData(-1000, ReputationLevel.Neutral)]
    [InlineData(0, ReputationLevel.Friendly)]
    [InlineData(600, ReputationLevel.Honored)]
    [InlineData(3000, ReputationLevel.Revered)]
    [InlineData(6000, ReputationLevel.Exalted)]
    [InlineData(12000, null)]
    public void NextLevel_WalksTheWholeLadder(int points, ReputationLevel? expected)
    {
        // Arrange
        var standing = new ReputationStanding { ReputationPoints = points };

        // Act & Assert
        standing.NextLevel.Should().Be(expected);
    }
}
