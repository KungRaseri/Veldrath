using FluentAssertions;
using RealmEngine.Shared.Models;
using Xunit;

namespace RealmEngine.Shared.Tests.Models;

/// <summary>
/// Behaviour tests for the socketable item models — crystals, essences, gems, orbs and runes —
/// plus the socket slot and its UI projection.
/// </summary>
[Trait("Category", "Unit")]
public class SocketableModelTests
{
    // ── Crystal ──────────────────────────────────────────────────────────────

    [Fact]
    public void Crystal_DefaultsToACrystalSocketAndCommonRarity()
    {
        // Act
        var crystal = new Crystal();

        // Assert
        crystal.Id.Should().NotBeNullOrEmpty();
        crystal.SocketType.Should().Be(SocketType.Crystal);
        crystal.Rarity.Should().Be(ItemRarity.Common);
        crystal.RarityWeight.Should().Be(50);
        crystal.Traits.Should().BeEmpty();
        crystal.Category.Should().BeNull();
    }

    [Fact]
    public void Crystal_GetDisplayName_SummarisesCategoryAndTraits()
    {
        // Arrange
        var crystal = new Crystal { Name = "Life Crystal", Category = "life" };
        crystal.Traits["maxHealth"] = new TraitValue(25, TraitType.Number);
        crystal.Traits["regen"] = new TraitValue("slow", TraitType.String);

        // Act
        var name = crystal.GetDisplayName();

        // Assert
        name.Should().Be("Life Crystal (life Crystal) - maxHealth: 25, regen: slow");
    }

    [Fact]
    public void Crystal_GetDisplayName_WithoutTraitsOrCategory_StillRenders()
    {
        // Act
        var name = new Crystal { Name = "Plain Crystal" }.GetDisplayName();

        // Assert
        name.Should().Be("Plain Crystal ( Crystal) - ");
    }

    // ── Essence ──────────────────────────────────────────────────────────────

    [Fact]
    public void Essence_DefaultsToCommonRarityAndIsNotSocketable()
    {
        // Act
        var essence = new Essence();

        // Assert
        essence.Id.Should().NotBeNullOrEmpty();
        essence.Rarity.Should().Be(ItemRarity.Common);
        essence.RarityWeight.Should().Be(50);
        essence.Should().NotBeAssignableTo<ISocketable>();
    }

    [Fact]
    public void Essence_GetDisplayName_SummarisesCategoryAndTraits()
    {
        // Arrange
        var essence = new Essence { Name = "Ember Essence", Category = "fire" };
        essence.Traits["potency"] = new TraitValue(3, TraitType.Number);

        // Act
        var name = essence.GetDisplayName();

        // Assert
        name.Should().Be("Ember Essence (fire Essence) - potency: 3");
    }

    // ── Gem ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Gem_DefaultsToAGemSocket()
    {
        // Act
        var gem = new Gem();

        // Assert
        gem.SocketType.Should().Be(SocketType.Gem);
        gem.RarityWeight.Should().Be(50);
        gem.Color.Should().Be(GemColor.Red);
    }

    [Fact]
    public void Gem_GetDisplayName_UsesTheLegacyColourLabel()
    {
        // Arrange
        var gem = new Gem { Name = "Ruby", Color = GemColor.Red };
        gem.Traits["strength"] = new TraitValue(2, TraitType.Number);

        // Act
        var name = gem.GetDisplayName();

        // Assert
        name.Should().Be("Ruby (Red Gem) - strength: 2");
    }

    // ── Orb ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Orb_DefaultsToAnOrbSocket()
    {
        // Act
        var orb = new Orb();

        // Assert
        orb.SocketType.Should().Be(SocketType.Orb);
        orb.RarityWeight.Should().Be(50);
    }

    [Fact]
    public void Orb_GetDisplayName_SummarisesCategoryAndTraits()
    {
        // Arrange
        var orb = new Orb { Name = "Strike Orb", Category = "combat" };

        // Act
        var name = orb.GetDisplayName();

        // Assert
        name.Should().Be("Strike Orb (combat Orb) - ");
    }

    // ── Rune ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Rune_DefaultsToARuneSocket()
    {
        // Act
        var rune = new Rune();

        // Assert
        rune.SocketType.Should().Be(SocketType.Rune);
        rune.RarityWeight.Should().Be(50);
    }

    [Fact]
    public void Rune_GetDisplayName_SummarisesCategoryAndTraits()
    {
        // Arrange
        var rune = new Rune { Name = "Fury Rune", Category = "offensive" };
        rune.Traits["procChance"] = new TraitValue(0.15, TraitType.Number);

        // Act
        var name = rune.GetDisplayName();

        // Assert
        name.Should().StartWith("Fury Rune (offensive Rune) - procChance: ");
    }

    // ── GemSocket (legacy) ──────────────────────────────────────────────────

    [Fact]
    public void GemSocket_GetDisplayName_ShowsTheSocketedGemWhenFilled()
    {
        // Arrange
        var socket = new GemSocket { Color = GemColor.Blue, Gem = new Gem { Name = "Sapphire" } };

        // Act
        var name = socket.GetDisplayName();

        // Assert
        name.Should().Be("[Blue Socket: Sapphire]");
    }

    [Fact]
    public void GemSocket_GetDisplayName_ReportsLockedWhenEmptyAndLocked()
    {
        // Arrange
        var socket = new GemSocket { Color = GemColor.Green, IsLocked = true };

        // Act
        var name = socket.GetDisplayName();

        // Assert
        name.Should().Be("[Green Socket: Locked]");
    }

    [Fact]
    public void GemSocket_GetDisplayName_ReportsEmptyWhenOpen()
    {
        // Act
        var name = new GemSocket { Color = GemColor.Yellow }.GetDisplayName();

        // Assert
        name.Should().Be("[Yellow Socket: Empty]");
    }

    [Fact]
    public void GemSocket_DefaultsToUnlockedAndEmpty()
    {
        // Act
        var socket = new GemSocket();

        // Assert
        socket.IsLocked.Should().BeFalse();
        socket.Gem.Should().BeNull();
    }

    // ── Socket ───────────────────────────────────────────────────────────────

    [Fact]
    public void Socket_DefaultsToUnlockedAndUnlinked()
    {
        // Act
        var socket = new Socket { Type = SocketType.Gem };

        // Assert
        socket.IsLocked.Should().BeFalse();
        socket.LinkGroup.Should().Be(-1);
        socket.Content.Should().BeNull();
    }

    [Fact]
    public void Socket_CanAccept_AcceptsMatchingContentInAnEmptyUnlockedSlot()
    {
        // Arrange
        var socket = new Socket { Type = SocketType.Gem };

        // Act & Assert
        socket.CanAccept(new Gem()).Should().BeTrue();
    }

    [Fact]
    public void Socket_CanAccept_RejectsContentOfAnotherType()
    {
        // Arrange
        var socket = new Socket { Type = SocketType.Gem };

        // Act & Assert
        socket.CanAccept(new Rune()).Should().BeFalse();
    }

    [Fact]
    public void Socket_CanAccept_RejectsALockedSlot()
    {
        // Arrange
        var socket = new Socket { Type = SocketType.Gem, IsLocked = true };

        // Act & Assert
        socket.CanAccept(new Gem()).Should().BeFalse();
    }

    [Fact]
    public void Socket_CanAccept_RejectsAnAlreadyFilledSlot()
    {
        // Arrange
        var socket = new Socket { Type = SocketType.Gem, Content = new Gem { Name = "Ruby" } };

        // Act & Assert
        socket.CanAccept(new Gem()).Should().BeFalse();
    }

    [Fact]
    public void Socket_GetDisplayName_ShowsContentWhenFilled()
    {
        // Arrange
        var socket = new Socket { Type = SocketType.Rune, Content = new Rune { Name = "Fury" } };

        // Act & Assert
        socket.GetDisplayName().Should().Be("[Rune Socket: Fury]");
    }

    [Fact]
    public void Socket_GetDisplayName_ShowsLockedWhenEmptyAndLocked()
    {
        // Arrange
        var socket = new Socket { Type = SocketType.Orb, IsLocked = true };

        // Act & Assert
        socket.GetDisplayName().Should().Be("[Orb Socket: Locked]");
    }

    [Fact]
    public void Socket_GetDisplayName_ShowsEmptyWhenOpen()
    {
        // Arrange
        var socket = new Socket { Type = SocketType.Crystal };

        // Act & Assert
        socket.GetDisplayName().Should().Be("[Crystal Socket: Empty]");
    }

    // ── SocketInfo ───────────────────────────────────────────────────────────

    [Fact]
    public void SocketInfo_DisplayText_ReportsFilledOverTotal()
    {
        // Arrange
        var info = new SocketInfo { Type = SocketType.Gem, FilledCount = 1, TotalCount = 2 };

        // Act & Assert
        info.DisplayText.Should().Be("Gem: 1/2");
    }

    [Fact]
    public void SocketInfo_HasEmptySlots_IsTrueWhileAnySlotIsOpen()
    {
        // Arrange
        var partial = new SocketInfo { FilledCount = 1, TotalCount = 2 };
        var full = new SocketInfo { FilledCount = 2, TotalCount = 2 };

        // Act & Assert
        partial.HasEmptySlots.Should().BeTrue();
        full.HasEmptySlots.Should().BeFalse();
    }

    [Fact]
    public void SocketInfo_IsFullySocketed_IsTrueOnlyWhenEverySlotIsFilled()
    {
        // Arrange
        var full = new SocketInfo { FilledCount = 3, TotalCount = 3 };
        var partial = new SocketInfo { FilledCount = 2, TotalCount = 3 };

        // Act & Assert
        full.IsFullySocketed.Should().BeTrue();
        partial.IsFullySocketed.Should().BeFalse();
    }

    [Fact]
    public void SocketInfo_FillPercentage_IsTheFilledFraction()
    {
        // Arrange
        var info = new SocketInfo { FilledCount = 1, TotalCount = 4 };

        // Act & Assert
        info.FillPercentage.Should().Be(0.25f);
    }

    [Fact]
    public void SocketInfo_FillPercentage_WithNoSockets_IsZero()
    {
        // Act & Assert
        new SocketInfo().FillPercentage.Should().Be(0f);
    }
}
