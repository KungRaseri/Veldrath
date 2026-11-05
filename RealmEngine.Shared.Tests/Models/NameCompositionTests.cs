using FluentAssertions;
using RealmEngine.Shared.Models;
using Xunit;

namespace RealmEngine.Shared.Tests.Models;

/// <summary>
/// Behaviour tests for the name-composition helpers shared by NPCs, enemies and powers:
/// component lookup by token and ordered name assembly.
/// </summary>
[Trait("Category", "Unit")]
public class NameCompositionTests
{
    private static NameComponent Component(string token, string value) => new() { Token = token, Value = value };

    // ── NPC ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Npc_GetPrefixValue_ReturnsTheValueForTheMatchingToken()
    {
        // Arrange
        var npc = new NPC();
        npc.Prefixes.Add(Component("title_prefix", "Elder"));
        npc.Prefixes.Add(Component("rank", "High"));

        // Act & Assert
        npc.GetPrefixValue("rank").Should().Be("High");
        npc.GetPrefixValue("missing").Should().BeNull();
    }

    [Fact]
    public void Npc_GetSuffixValue_ReturnsTheValueForTheMatchingToken()
    {
        // Arrange
        var npc = new NPC();
        npc.Suffixes.Add(Component("title_suffix", "the Wise"));

        // Act & Assert
        npc.GetSuffixValue("title_suffix").Should().Be("the Wise");
        npc.GetSuffixValue("other").Should().BeNull();
    }

    [Fact]
    public void Npc_ComposeNameFromComponents_JoinsPrefixesBaseNameAndSuffixes()
    {
        // Arrange
        var npc = new NPC { BaseName = "Garrick" };
        npc.Prefixes.Add(Component("title_prefix", "Elder"));
        npc.Suffixes.Add(Component("title_suffix", "the Wise"));

        // Act
        var name = npc.ComposeNameFromComponents();

        // Assert
        name.Should().Be("Elder Garrick the Wise");
    }

    [Fact]
    public void Npc_ComposeNameFromComponents_SkipsBlankComponentsAndMissingBaseName()
    {
        // Arrange
        var npc = new NPC { BaseName = "   " };
        npc.Prefixes.Add(Component("a", ""));
        npc.Prefixes.Add(Component("b", "Elder"));
        npc.Suffixes.Add(Component("c", ""));

        // Act
        var name = npc.ComposeNameFromComponents();

        // Assert
        name.Should().Be("Elder");
    }

    [Fact]
    public void Npc_ComposeNameFromComponents_WithNoComponents_ReturnsEmptyString()
    {
        // Act & Assert
        new NPC().ComposeNameFromComponents().Should().BeEmpty();
    }

    [Fact]
    public void Npc_PreservesBothPrefixAndSuffixOrder()
    {
        // Arrange
        var npc = new NPC { BaseName = "Elara" };
        npc.Prefixes.Add(Component("size", "Tall"));
        npc.Prefixes.Add(Component("title", "Wise"));
        npc.Suffixes.Add(Component("of", "the Glade"));
        npc.Suffixes.Add(Component("epithet", "Keeper"));

        // Act
        var name = npc.ComposeNameFromComponents();

        // Assert
        name.Should().Be("Tall Wise Elara the Glade Keeper");
    }

    // ── Enemy ────────────────────────────────────────────────────────────────

    [Fact]
    public void Enemy_GetPrefixValue_ReturnsTheValueForTheMatchingToken()
    {
        // Arrange
        var enemy = new Enemy();
        enemy.Prefixes.Add(Component("size", "Giant"));

        // Act & Assert
        enemy.GetPrefixValue("size").Should().Be("Giant");
        enemy.GetPrefixValue("element").Should().BeNull();
    }

    [Fact]
    public void Enemy_GetSuffixValue_ReturnsTheValueForTheMatchingToken()
    {
        // Arrange
        var enemy = new Enemy();
        enemy.Suffixes.Add(Component("title", "the Unbroken"));

        // Act & Assert
        enemy.GetSuffixValue("title").Should().Be("the Unbroken");
        enemy.GetSuffixValue("missing").Should().BeNull();
    }

    [Fact]
    public void Enemy_ComposeNameFromComponents_JoinsPrefixesBaseNameAndSuffixes()
    {
        // Arrange
        var enemy = new Enemy { BaseName = "Wolf" };
        enemy.Prefixes.Add(Component("size", "Giant"));
        enemy.Suffixes.Add(Component("title", "of the North"));

        // Act
        var name = enemy.ComposeNameFromComponents();

        // Assert
        name.Should().Be("Giant Wolf of the North");
    }

    [Fact]
    public void Enemy_ComposeNameFromComponents_WithOnlyABaseName_ReturnsTheBaseName()
    {
        // Act & Assert
        new Enemy { BaseName = "Wolf" }.ComposeNameFromComponents().Should().Be("Wolf");
    }

    [Fact]
    public void Enemy_ComposeNameFromComponents_WithNoNameParts_ReturnsEmptyString()
    {
        // Act & Assert
        new Enemy().ComposeNameFromComponents().Should().BeEmpty();
    }

    // ── Power ────────────────────────────────────────────────────────────────

    [Fact]
    public void Power_GetPrefixValue_ReturnsTheValueForTheMatchingToken()
    {
        // Arrange
        var power = new Power { Name = "fireball" };
        power.Prefixes.Add(Component("element", "Blazing"));

        // Act & Assert
        power.GetPrefixValue("element").Should().Be("Blazing");
        power.GetPrefixValue("missing").Should().BeNull();
    }

    [Fact]
    public void Power_ComposeDisplayNameFromComponents_JoinsPrefixesAndBaseName()
    {
        // Arrange
        var power = new Power { BasePowerName = "Fireball" };
        power.Prefixes.Add(Component("element", "Blazing"));
        power.Prefixes.Add(Component("rank", "Greater"));

        // Act
        var name = power.ComposeDisplayNameFromComponents();

        // Assert
        name.Should().Be("Blazing Greater Fireball");
    }

    [Fact]
    public void Power_ComposeDisplayNameFromComponents_SkipsBlankEntries()
    {
        // Arrange
        var power = new Power { BasePowerName = "  " };
        power.Prefixes.Add(Component("element", "Blazing"));
        power.Prefixes.Add(Component("empty", " "));

        // Act
        var name = power.ComposeDisplayNameFromComponents();

        // Assert
        name.Should().Be("Blazing");
    }

    [Fact]
    public void Power_ComposeDisplayNameFromComponents_WithNoComponents_ReturnsEmptyString()
    {
        // Act & Assert
        new Power().ComposeDisplayNameFromComponents().Should().BeEmpty();
    }

    [Fact]
    public void Power_DefaultsToTalentsWithTierOneAndNoTradition()
    {
        // Act
        var power = new Power();

        // Assert
        power.Type.Should().Be(PowerType.Talent);
        power.EffectType.Should().Be(PowerEffectType.None);
        power.Tier.Should().Be(1);
        power.RarityWeight.Should().Be(1);
        power.RequiredLevel.Should().Be(1);
        power.Rank.Should().Be(0);
        power.MinimumSkillRank.Should().Be(0);
        power.SelectionWeight.Should().Be(50);
        power.Tradition.Should().BeNull();
        power.School.Should().BeNull();
        power.IsPassive.Should().BeFalse();
        power.Prefixes.Should().BeEmpty();
        power.AllowedClasses.Should().BeEmpty();
        power.Traits.Should().BeEmpty();
    }
}
