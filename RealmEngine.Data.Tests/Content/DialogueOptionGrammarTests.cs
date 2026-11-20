using FluentAssertions;
using RealmEngine.Data.Content;

namespace RealmEngine.Data.Tests.Content;

/// <summary>
/// Tests for the requirement/grant grammar. The validator and the runtime parse with this one implementation, so a
/// case accepted here is a case the game will act on.
/// </summary>
public class DialogueOptionGrammarTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_block_has_no_entries(string? block)
    {
        DialogueOptionGrammar.Split(block).Should().BeEmpty();
    }

    [Fact]
    public void Newlines_and_semicolons_both_separate_entries_and_blank_lines_are_dropped()
    {
        var entries = DialogueOptionGrammar.Split("level:3\n\nitem:glowcap-mushroom:2; gold:25\r\n  ");

        entries.Should().Equal("level:3", "item:glowcap-mushroom:2", "gold:25");
    }

    [Theory]
    [InlineData("level:3", DialogueEffectKind.Level, "", null, 3)]
    [InlineData("item:glowcap-mushroom:2", DialogueEffectKind.Item, "glowcap-mushroom", null, 2)]
    [InlineData("quest:active:the-lost-shipment", DialogueEffectKind.QuestActive, "the-lost-shipment", null, 0)]
    [InlineData("quest:complete:the-lost-shipment", DialogueEffectKind.QuestComplete, "the-lost-shipment", null, 0)]
    [InlineData("objective:talk:village-blacksmith:1", DialogueEffectKind.Objective, "talk", "village-blacksmith", 1)]
    public void Requirements_parse_into_their_parts(
        string entry,
        DialogueEffectKind kind,
        string key,
        string? detail,
        int amount)
    {
        DialogueOptionGrammar.TryParseRequirement(entry, out var effect, out var error)
            .Should().BeTrue(error);

        effect.Kind.Should().Be(kind);
        effect.Key.Should().Be(key);
        effect.Detail.Should().Be(detail);
        effect.Amount.Should().Be(amount);
    }

    [Theory]
    [InlineData("item:healing-draught:1", DialogueEffectKind.Item, "healing-draught", 1)]
    [InlineData("gold:25", DialogueEffectKind.Gold, "", 25)]
    [InlineData("quest-start:the-lost-shipment", DialogueEffectKind.QuestStart, "the-lost-shipment", 0)]
    [InlineData("objective:talk:wayfarer-elder:1", DialogueEffectKind.Objective, "talk", 1)]
    public void Grants_parse_into_their_parts(
        string entry,
        DialogueEffectKind kind,
        string key,
        int amount)
    {
        DialogueOptionGrammar.TryParseGrant(entry, out var effect, out var error)
            .Should().BeTrue(error);

        effect.Kind.Should().Be(kind);
        effect.Key.Should().Be(key);
        effect.Amount.Should().Be(amount);
    }

    [Theory]
    [InlineData("gold:25")]
    [InlineData("quest-start:the-lost-shipment")]
    [InlineData("mana:5")]
    [InlineData("level:0")]
    [InlineData("level:three")]
    [InlineData("item:glowcap-mushroom")]
    [InlineData("item:Glowcap-Mushroom:1")]
    [InlineData("item:glowcap-mushroom:0")]
    [InlineData("quest:done:the-lost-shipment")]
    [InlineData("objective:talk")]
    [InlineData("")]
    public void A_bad_requirement_is_refused_with_a_reason(string entry)
    {
        DialogueOptionGrammar.TryParseRequirement(entry, out _, out var error).Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("level:3")]
    [InlineData("quest:active:the-lost-shipment")]
    [InlineData("gold:0")]
    [InlineData("quest-start")]
    [InlineData("nonsense:1")]
    public void A_bad_grant_is_refused_with_a_reason(string entry)
    {
        DialogueOptionGrammar.TryParseGrant(entry, out _, out var error).Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Requirement_and_grant_kinds_are_disjoint_where_they_should_be()
    {
        // Item and objective appear on both sides; everything else belongs to exactly one.
        DialogueOptionGrammar.RequirementKinds.Should().Contain([
            DialogueEffectKind.Level,
            DialogueEffectKind.Item,
            DialogueEffectKind.QuestActive,
            DialogueEffectKind.QuestComplete,
            DialogueEffectKind.Objective,
        ]);

        DialogueOptionGrammar.GrantKinds.Should().Contain([
            DialogueEffectKind.Item,
            DialogueEffectKind.Gold,
            DialogueEffectKind.QuestStart,
            DialogueEffectKind.Objective,
        ]);

        DialogueOptionGrammar.RequirementKinds.Should().NotContain([
            DialogueEffectKind.Gold,
            DialogueEffectKind.QuestStart,
        ]);

        DialogueOptionGrammar.GrantKinds.Should().NotContain([
            DialogueEffectKind.Level,
            DialogueEffectKind.QuestActive,
            DialogueEffectKind.QuestComplete,
        ]);
    }

    [Fact]
    public void An_effect_describes_itself_in_player_words()
    {
        // The client shows this wording next to the choice, so it has to read like the requirement it enforces.
        DialogueOptionGrammar.TryParseGrant("item:glowcap-mushroom:2", out var item, out _);
        DialogueOptionGrammar.TryParseRequirement("level:3", out var level, out _);
        DialogueOptionGrammar.TryParseGrant("gold:25", out var gold, out _);
        DialogueOptionGrammar.TryParseRequirement("quest:active:the-lost-shipment", out var quest, out _);
        DialogueOptionGrammar.TryParseRequirement("quest:complete:the-lost-shipment", out var done, out _);
        DialogueOptionGrammar.TryParseGrant("objective:talk:village-blacksmith:1", out var objective, out _);

        item.Describe().Should().Be("2 × glowcap-mushroom");
        level.Describe().Should().Be("level 3");
        gold.Describe().Should().Be("25 gold");
        quest.Describe().Should().Be("quest the-lost-shipment in progress");
        done.Describe().Should().Be("quest the-lost-shipment complete");
        objective.Describe().Should().Be("talk village-blacksmith");

        // A counted objective keeps its count, because one of something and five of something read differently.
        DialogueOptionGrammar.TryParseGrant("objective:kill:goblin-scout:5", out var counted, out _);
        counted.Describe().Should().Be("kill goblin-scout ×5");
    }
}
